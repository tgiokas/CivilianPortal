using Microsoft.EntityFrameworkCore;

using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Interfaces;
using CitizenPortal.Infrastructure.Database;

namespace CitizenPortal.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly ApplicationDbContext _dbContext;

    public OutboxRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<OutboxMessage>> GetPendingAsync(int batchSize = 20)
    {
        return await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(o => o.ProcessedAt == null && o.RetryCount < 5)
            .OrderBy(o => o.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    // No SaveChanges, caller commits the transaction alongside the Application insert.
    public async Task AddAsync(OutboxMessage message)
    {
        await _dbContext.OutboxMessages.AddAsync(message);
    }

    public async Task MarkAsProcessedAsync(int id)
    {
        await _dbContext.OutboxMessages
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ProcessedAt, DateTime.UtcNow));
    }

    public async Task MarkAsFailedAsync(int id, string error)
    {
        await _dbContext.OutboxMessages
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.RetryCount, o => o.RetryCount + 1)
            .SetProperty(a => a.Error, error));
    }
}
