using ClarityOS.ContentApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClarityOS.ContentApi.Data.Repositories;

public class ProposalRepository(AppDbContext db) : IProposalRepository
{
    public async Task<IEnumerable<AiProposal>> GetAllAsync() =>
        await db.AiProposals.ToListAsync();

    public async Task<AiProposal?> GetByIdAsync(Guid id) =>
        await db.AiProposals.FindAsync(id);

    public async Task AddAsync(AiProposal proposal)
    {
        await db.AiProposals.AddAsync(proposal);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(AiProposal proposal)
    {
        db.AiProposals.Update(proposal);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(AiProposal proposal)
    {
        db.AiProposals.Remove(proposal);
        await db.SaveChangesAsync();
    }
}
