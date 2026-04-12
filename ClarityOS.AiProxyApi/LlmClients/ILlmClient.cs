namespace ClarityOS.AiProxyApi.LlmClients;

public interface ILlmClient
{
    Task<(string Response, string Model)> GenerateAsync(string systemPrompt, string userPrompt, string? modelOverride = null);
    IReadOnlyList<string> AvailableModels { get; }
}
