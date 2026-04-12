namespace ClarityOS.AiProxyApi.DTOs;

public record GenerateRequest(
    List<TaskSummary> Tasks,
    string UserPrompt
);

public record TaskSummary(
    string TaskId,
    string Title,
    string? Description
);
