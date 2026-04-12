namespace ClarityOS.ContentApi.Data.Entities;

public class AiProposal
{
    public Guid Id { get; set; }
    public Guid OriginalTaskId { get; set; }
    public required string ProposedTitle { get; set; }
    public string? ProposedDescription { get; set; }
    public DateTime ProposedDueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public ProposalStatus Status { get; set; }

    public ClarityTask OriginalTask { get; set; } = null!;
}
