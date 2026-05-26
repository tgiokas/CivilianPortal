using Microsoft.EntityFrameworkCore;

using CitizenPortal.Domain.Enums;
using CitizenPortal.Domain.Interfaces;
using CitizenPortal.Infrastructure.Database;

namespace CitizenPortal.Infrastructure.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ApplicationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Domain.Entities.Application?> GetByPublicIdAsync(Guid publicId)
    {
        return await _dbContext.Applications
            .AsNoTracking()
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.PublicId == publicId);
    }

    public async Task<List<Domain.Entities.Application>> GetByUserIdAsync(int userId, int skip = 0, int take = 50)
    {
        // Cap take to a hard ceiling so an over-eager caller can't load
        // the entire table (plus joined documents) in one query.
        const int maxTake = 200;
        if (skip < 0) skip = 0;
        if (take <= 0) take = 50;
        if (take > maxTake) take = maxTake;

        return await _dbContext.Applications
            .AsNoTracking()
            .Include(a => a.Documents)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    // No SaveChanges, caller commits the transaction (outbox pattern).
    public async Task AddWithoutSaveAsync (Domain.Entities.Application application)
    {
        await _dbContext.Applications.AddAsync(application);
    }

    public async Task<bool> UpdateStatusAsync(int applicationId, ApplicationStatus status, string protocolNumber)
    {
        var rows = await _dbContext.Applications
            .Where(a => a.Id == applicationId
                     && a.ProtocolNumber == null)   // <-- only write once
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, status)
                .SetProperty(a => a.ProtocolNumber, protocolNumber)
                .SetProperty(a => a.ModifiedAt, DateTime.UtcNow));
        return rows > 0;
    }
}