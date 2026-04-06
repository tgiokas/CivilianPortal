using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Domain.Interfaces;

public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(int id);
    Task<Application?> GetByPublicIdAsync(Guid publicId);
    Task<List<Application>> GetByCitizenUserIdAsync(int citizenUserId);
    Task AddAsync(Application application);
    Task UpdateAsync(Application application);
    Task UpdateStatusAsync(int applicationId, ApplicationStatus status, string? protocolNumber = null);
}
