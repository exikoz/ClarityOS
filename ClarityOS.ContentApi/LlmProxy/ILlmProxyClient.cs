using ClarityOS.ContentApi.DTOs;

namespace ClarityOS.ContentApi.LlmProxy;

public interface ILlmProxyClient
{
    Task<(string Response, string Model)> RequestRescheduleAsync(List<TaskResponse> tasks, string userPrompt);
}
