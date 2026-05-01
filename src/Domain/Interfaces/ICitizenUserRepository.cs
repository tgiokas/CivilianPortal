using CitizenPortal.Domain.Entities;

namespace CitizenPortal.Domain.Interfaces;

public interface ICitizenUserRepository
{
    Task<CitizenUser?> GetByKeycloakUserIdReadOnlyAsync(Guid keycloakUserId);
    Task<CitizenUser?> GetByKeycloakUserIdAsync(Guid keycloakUserId);
    Task<(CitizenUser User, bool Created)> GetOrCreateAsync(CitizenUser newUser);    
    Task UpdateAsync(CitizenUser user);
}
