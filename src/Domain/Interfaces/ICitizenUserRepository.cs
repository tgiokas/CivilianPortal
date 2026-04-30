using CitizenPortal.Domain.Entities;

namespace CitizenPortal.Domain.Interfaces;

public interface ICitizenUserRepository
{
    Task<CitizenUser?> GetByIdAsync(int id);
    Task<CitizenUser?> GetByKeycloakUserIdAsync(Guid keycloakUserId); 
    Task AddAsync(CitizenUser user);
    Task UpdateAsync(CitizenUser user);
}
