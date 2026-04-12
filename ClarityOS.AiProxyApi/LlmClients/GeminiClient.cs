using System.Text;
using System.Text.Json;
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

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            logger.LogError("Gemini API error: {StatusCode} - {Body}", response.StatusCode, errorBody);
            throw new HttpRequestException($"Gemini API error: {response.StatusCode}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseContent);

        var candidates = document.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() == 0) return ("[]", model);

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        var text = parts[0].GetProperty("text").GetString() ?? "[]";
        return (text, model);
    }
}
