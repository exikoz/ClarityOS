using ClarityOS.ContentApi.Data.Entities;

namespace ClarityOS.ContentApi.Data.Repositories;

public interface ITaskRepository
{
    Task<IEnumerable<ClarityTask>> GetAllAsync();
    Task<ClarityTask?> GetByIdAsync(Guid id);
    Task AddAsync(ClarityTask task);
    Task UpdateAsync(ClarityTask task);
    Task DeleteAsync(ClarityTask task);
}
