namespace ClarityOS.AiProxyApi.DTOs;

public record GenerateRequest(
    List<TaskSummary> Tasks,
    string UserPrompt,
    string? Model = null
);

public record TaskSummary(
    string TaskId,
    string Title,
    string? Description
);
