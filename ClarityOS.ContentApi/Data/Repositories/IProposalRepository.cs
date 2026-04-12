using ClarityOS.ContentApi.Data.Entities;

namespace ClarityOS.ContentApi.Data.Repositories;

public interface IProposalRepository
{
    Task<IEnumerable<AiProposal>> GetAllAsync();
    Task<AiProposal?> GetByIdAsync(Guid id);
    Task AddAsync(AiProposal proposal);
    Task UpdateAsync(AiProposal proposal);
    Task DeleteAsync(AiProposal proposal);
}
