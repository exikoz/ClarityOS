using ClarityOS.ContentApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClarityOS.ContentApi.Data.Repositories;

public class TaskRepository(AppDbContext db) : ITaskRepository
{
    public async Task<IEnumerable<ClarityTask>> GetAllAsync() =>
        await db.Tasks.ToListAsync();

    public async Task<ClarityTask?> GetByIdAsync(Guid id) =>
        await db.Tasks.FindAsync(id);

    public async Task AddAsync(ClarityTask task)
    {
        await db.Tasks.AddAsync(task);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(ClarityTask task)
    {
        db.Tasks.Update(task);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(ClarityTask task)
    {
        db.Tasks.Remove(task);
        await db.SaveChangesAsync();
    }
}
