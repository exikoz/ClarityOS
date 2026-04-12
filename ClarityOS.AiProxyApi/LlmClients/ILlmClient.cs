namespace ClarityOS.AiProxyApi.LlmClients;

public interface ILlmClient
{
    Task<string> GenerateAsync(string systemPrompt, string userPrompt);
}
