using System.Net.Http.Json;
using ClarityOS.AiProxyApi.DTOs;

namespace ClarityOS.AiProxyApi.LlmClients;

public class OllamaClient(HttpClient httpClient, IConfiguration config) : ILlmClient
{
    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt)
    {
        var model = config["Ollama:Model"] ?? "llama3.2";
        var combinedPrompt = $"{systemPrompt}\n\n{userPrompt}";

        var request = new OllamaRequest(model, combinedPrompt, Stream: false);

        var response = await httpClient.PostAsJsonAsync("api/generate", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        return result?.Response ?? string.Empty;
    }
}
