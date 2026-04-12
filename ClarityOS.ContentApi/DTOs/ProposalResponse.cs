using ClarityOS.ContentApi.Data.Entities;

namespace ClarityOS.ContentApi.DTOs;

public record ProposalResponse(
    Guid Id,
    Guid OriginalTaskId,
    string ProposedTitle,
    string? ProposedDescription,
    DateTime ProposedDueDate,
    DateTime CreatedAt,
    ProposalStatus Status
);
