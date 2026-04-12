namespace ClarityOS.ContentApi.DTOs;

public record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    string? Category,
    DateTime DueDate,
    bool IsCompleted,
    DateTime CreatedAt
);
