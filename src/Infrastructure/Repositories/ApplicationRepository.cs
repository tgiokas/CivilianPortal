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

    public async Task<List<Domain.Entities.Application>> GetByCitizenUserIdAsync(int citizenUserId)
    {
        return await _dbContext.Applications
            .AsNoTracking()
            .Include(a => a.Documents)
            .Where(a => a.CitizenUserId == citizenUserId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    // No SaveChanges — caller commits the transaction (outbox pattern).
    public async Task AddWithoutSaveAsync (Domain.Entities.Application application)
    {
        await _dbContext.Applications.AddAsync(application);
    }

    public async Task<bool> UpdateStatusAsync(int applicationId, ApplicationStatus status, string? protocolNumber = null)
    {
        var application = await _dbContext.Applications.FindAsync(applicationId);
        if (application is null)
        {
            return false;
        }

        application.Status = status;
        application.ProtocolNumber = protocolNumber;
        application.ModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return true;
    }   
}