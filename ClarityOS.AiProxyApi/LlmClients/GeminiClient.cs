using System.Net;
using System.Text;
using System.Text.Json;
using ClarityOS.AiProxyApi.Exceptions;
using ClarityOS.AiProxyApi.Options;
using Microsoft.Extensions.Options;

namespace ClarityOS.AiProxyApi.LlmClients;

public class GeminiClient(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiClient> logger) : ILlmClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string DefaultModel = "gemini-3.1-flash-lite-preview";
    private readonly GeminiOptions _options = options.Value;

    private static readonly IReadOnlyList<string> Models = new[]
    {
        "gemini-3.1-flash-lite-preview",
        "gemini-2.5-flash-preview-05-20",
        "gemini-2.5-pro-preview-05-06"
    };

    public IReadOnlyList<string> AvailableModels => Models;

    public async Task<(string Response, string Model)> GenerateAsync(string systemPrompt, string userPrompt, string? modelOverride = null)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
            throw new InvalidOperationException("Gemini:ApiKey is not configured.");

        var model = modelOverride ?? DefaultModel;
        var url = $"{BaseUrl}/{model}:generateContent";

        var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = fullPrompt } } } },
            generationConfig = new { maxOutputTokens = 1024, temperature = 0.1 }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !ex.CancellationToken.IsCancellationRequested)
        {
            logger.LogError("Gemini API request timed out for model {Model}", model);
            throw new TimeoutException($"Request to Gemini API timed out for model: {model}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            logger.LogError("Gemini API returned {StatusCode}. Response body logged for diagnostics.", (int)response.StatusCode);

            switch (response.StatusCode)
            {
                case HttpStatusCode.TooManyRequests:
                    var retryAfter = response.Headers.RetryAfter?.Delta;
                    throw new RateLimitException("Gemini API rate limit exceeded. Try again later.", retryAfter);

                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    throw new ExternalAuthException("Authentication with Gemini API failed. Check API key configuration.");

                default:
                    throw new ExternalServiceException(
                        $"Gemini API returned an error (HTTP {(int)response.StatusCode}).",
                        (int)response.StatusCode);
            }
        }

        var responseContent = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(responseContent))
            throw new ExternalServiceException("Gemini API returned an empty response.");

        using var document = JsonDocument.Parse(responseContent);

        if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.GetArrayLength() == 0)
        {
            logger.LogWarning("Gemini API returned no candidates for model {Model}", model);
            throw new ExternalServiceException("Gemini API returned no candidates in the response.");
        }

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        var text = parts[0].GetProperty("text").GetString() ?? "[]";
        return (text, model);
    }
}
