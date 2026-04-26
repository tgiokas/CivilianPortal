using CitizenPortal.Domain.Entities;

namespace CitizenPortal.Domain.Interfaces;

public interface ICitizenUserRepository
{
    Task AddAsync(CitizenUser user);
    Task<CitizenUser?> GetByEmailAsync(string email);
    Task<CitizenUser?> GetByIdAsync(int id);
    Task<CitizenUser?> GetByKeycloakUserIdAsync(Guid keycloakUserId);
    Task<CitizenUser?> GetByKeycloakUserIdReadOnlyAsync(Guid keycloakUserId);
    Task UpdateAsync(CitizenUser user);
}
