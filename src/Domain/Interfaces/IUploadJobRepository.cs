using CitizenPortal.Domain.Entities;

namespace CitizenPortal.Domain.Interfaces;

public interface IUploadJobRepository
{
    Task<UploadJob?> GetByJobIdAsync(Guid jobId);

    // No SaveChanges, caller commits the transaction (outbox pattern).
    Task AddWithoutSaveAsync(UploadJob job);

    Task<bool> MarkCompletedAsync(Guid jobId, long archiumFileId, string? protocolNumber, string? protocolYear);
    Task<bool> MarkFailedAsync(Guid jobId, string errorCode, string errorMessage);
}
