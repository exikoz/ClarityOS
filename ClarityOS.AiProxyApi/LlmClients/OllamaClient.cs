using System.Net.Http.Json;
using ClarityOS.AiProxyApi.DTOs;

namespace ClarityOS.AiProxyApi.LlmClients;

public class OllamaClient(HttpClient httpClient, IConfiguration config) : ILlmClient
{
    private static readonly IReadOnlyList<string> Models = new[]
    {
        "llama3.2",
        "llama3.2:1b",
        "phi3:mini"
    };

    public IReadOnlyList<string> AvailableModels => Models;

    public async Task<(string Response, string Model)> GenerateAsync(string systemPrompt, string userPrompt, string? modelOverride = null)
    {
        var model = modelOverride ?? config["Ollama:Model"] ?? "llama3.2";
        var combinedPrompt = $"{systemPrompt}\n\n{userPrompt}";

        var request = new OllamaRequest(model, combinedPrompt, Stream: false);

        var response = await httpClient.PostAsJsonAsync("api/generate", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        return (result?.Response ?? string.Empty, model);
    }
}
