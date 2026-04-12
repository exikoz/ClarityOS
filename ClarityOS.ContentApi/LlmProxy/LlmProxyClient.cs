using System.Net.Http.Json;
using ClarityOS.ContentApi.DTOs;

namespace ClarityOS.ContentApi.LlmProxy;

public class LlmProxyClient(HttpClient httpClient, IConfiguration config) : ILlmProxyClient
{
    public async Task<string> RequestRescheduleAsync(List<TaskResponse> tasks, string userPrompt)
    {
        var apiKey = config["LlmProxy:ApiKey"] ?? string.Empty;
        httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var taskSummaries = tasks.Select(t => new
        {
            taskId      = t.Id.ToString(),
            title       = t.Title,
            description = t.Description
        }).ToList();

        var payload = new { tasks = taskSummaries, userPrompt };

        var response = await httpClient.PostAsJsonAsync("api/llm/generate", payload);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
