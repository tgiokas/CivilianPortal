using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Domain.Interfaces;

public interface IApplicationRepository
{
    Task<Application?> GetByPublicIdAsync(Guid publicId);
    Task<List<Application>> GetByCitizenUserIdAsync(int citizenUserId);
    Task AddAsync(Application application);
    Task UpdateDocumentLocationsAsync(int applicationId, List<(string Bucket, string Key)> newLocations);
    Task UpdateStatusAsync(int applicationId, ApplicationStatus status, string? protocolNumber = null);
}
