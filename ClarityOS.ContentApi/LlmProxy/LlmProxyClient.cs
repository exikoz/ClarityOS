using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClarityOS.ContentApi.DTOs;

namespace ClarityOS.ContentApi.LlmProxy;

public class LlmProxyClient(HttpClient httpClient, IConfiguration config, ILogger<LlmProxyClient> logger) : ILlmProxyClient
{
    public async Task<(string Response, string Model)> RequestRescheduleAsync(List<TaskResponse> tasks, string userPrompt)
    {
        var apiKey = config["LlmProxy:ApiKey"] ?? string.Empty;

        var taskSummaries = tasks.Select(t => new
        {
            taskId      = t.Id.ToString(),
            title       = t.Title,
            description = t.Description
        }).ToList();

        var payload = new { tasks = taskSummaries, userPrompt };

        var request = new HttpRequestMessage(HttpMethod.Post, "api/llm/generate");
        request.Headers.Add("X-Api-Key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !ex.CancellationToken.IsCancellationRequested)
        {
            logger.LogError("Request to AiProxyApi timed out");
            throw new TimeoutException("The AI proxy service did not respond in time.");
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("AiProxyApi returned {StatusCode}", (int)response.StatusCode);
            throw new HttpRequestException(
                $"AI proxy returned HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var model = doc.RootElement.GetProperty("model").GetString() ?? "unknown";
        var llmResponse = doc.RootElement.GetProperty("response").GetString() ?? "[]";

        return (llmResponse, model);
    }
}
