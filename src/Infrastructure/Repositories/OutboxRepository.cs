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
        var message = await _dbContext.OutboxMessages.FindAsync(id);
        if (message is not null)
        {
            message.ProcessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task MarkAsFailedAsync(int id, string error)
    {
        var message = await _dbContext.OutboxMessages.FindAsync(id);
        if (message is not null)
        {
            message.RetryCount++;
            message.Error = error;
            await _dbContext.SaveChangesAsync();
        }
    }
}
