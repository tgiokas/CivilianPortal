using Microsoft.EntityFrameworkCore;

using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Enums;
using CitizenPortal.Domain.Interfaces;
using CitizenPortal.Infrastructure.Database;

namespace CitizenPortal.Infrastructure.Repositories;

public class UploadJobRepository : IUploadJobRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UploadJobRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UploadJob?> GetByJobIdAsync(Guid jobId)
    {
        return await _dbContext.UploadJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JobId == jobId);
    }

    public async Task AddWithoutSaveAsync(UploadJob job)
    {
        await _dbContext.UploadJobs.AddAsync(job);
    }

    public async Task<bool> MarkCompletedAsync(Guid jobId, long archiumFileId, string? protocolNumber, string? protocolYear)
    {
        var rows = await _dbContext.UploadJobs
            .Where(j => j.JobId == jobId
                     && j.Status == UploadJobStatus.Pending)   // <-- only write once
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, UploadJobStatus.Completed)
                .SetProperty(j => j.ArchiumFileId, archiumFileId)
                .SetProperty(j => j.ProtocolNumber, protocolNumber)
                .SetProperty(j => j.ProtocolYear, protocolYear)
                .SetProperty(j => j.CompletedAt, DateTime.UtcNow));
        return rows > 0;
    }

    public async Task<bool> MarkFailedAsync(Guid jobId, string errorCode, string errorMessage)
    {
        var rows = await _dbContext.UploadJobs
            .Where(j => j.JobId == jobId
                     && j.Status == UploadJobStatus.Pending)   // <-- only write once
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, UploadJobStatus.Failed)
                .SetProperty(j => j.ErrorCode, errorCode)
                .SetProperty(j => j.ErrorMessage, errorMessage)
                .SetProperty(j => j.CompletedAt, DateTime.UtcNow));
        return rows > 0;
    }
}
