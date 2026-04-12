namespace ClarityOS.ContentApi.Data.Entities;

public class ClarityTask
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<AiProposal> AiProposals { get; set; } = [];
}
