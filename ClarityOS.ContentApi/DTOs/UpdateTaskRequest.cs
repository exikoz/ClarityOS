namespace ClarityOS.ContentApi.DTOs;

public record UpdateTaskRequest(
    string Title,
    string? Description,
    string? Category,
    DateTime DueDate
);
