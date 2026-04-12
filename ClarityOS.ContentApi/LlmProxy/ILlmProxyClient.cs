using ClarityOS.ContentApi.DTOs;

namespace ClarityOS.ContentApi.LlmProxy;

public interface ILlmProxyClient
{
    Task<string> RequestRescheduleAsync(List<TaskResponse> tasks, string userPrompt);
}
