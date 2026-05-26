using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Domain.Interfaces;

public interface IApplicationRepository
{
    Task<Application?> GetByPublicIdAsync(Guid publicId);
    Task<List<Application>> GetByUserIdAsync(int citizenUserId, int skip = 0, int take = 50);
    Task AddWithoutSaveAsync(Application application);
    Task<bool> UpdateStatusAsync(int applicationId, ApplicationStatus status, string protocolNumber);
}
