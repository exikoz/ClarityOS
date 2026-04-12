using ClarityOS.ContentApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClarityOS.ContentApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ClarityTask> Tasks => Set<ClarityTask>();
    public DbSet<AiProposal> AiProposals => Set<AiProposal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiProposal>()
            .HasOne(p => p.OriginalTask)
            .WithMany(t => t.AiProposals)
            .HasForeignKey(p => p.OriginalTaskId);
    }
}
