using Microsoft.EntityFrameworkCore;

using CitizenPortal.Domain.Entities;
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

    public async Task<Domain.Entities.Application?> GetByIdAsync(int id)
    {
        return await _dbContext.Applications
            .AsNoTracking()
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == id);
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

    public async Task AddAsync(Domain.Entities.Application application)
    {
        await _dbContext.Applications.AddAsync(application);
    }

    public Task UpdateAsync(Domain.Entities.Application application)
    {
        application.ModifiedAt = DateTime.UtcNow;
        _dbContext.Applications.Update(application);
        return Task.CompletedTask;
    }

    public async Task UpdateStatusAsync(int applicationId, ApplicationStatus status, string? protocolNumber = null)
    {
        var application = await _dbContext.Applications.FindAsync(applicationId);
        if (application is not null)
        {
            application.Status = status;
            application.ProtocolNumber = protocolNumber ?? application.ProtocolNumber;
            application.ModifiedAt = DateTime.UtcNow;
        }
        await _dbContext.SaveChangesAsync();
    }
}
