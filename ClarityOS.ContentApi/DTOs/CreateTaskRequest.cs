namespace ClarityOS.ContentApi.DTOs;

public record CreateTaskRequest(
    string Title,
    string? Description,
    string? Category,
    DateTime DueDate
);
