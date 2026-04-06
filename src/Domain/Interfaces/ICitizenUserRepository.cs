using CitizenPortal.Domain.Entities;

namespace CitizenPortal.Domain.Interfaces;

public interface ICitizenUserRepository
{
    Task<CitizenUser?> GetByKeycloakUserIdAsync(Guid keycloakUserId);
    Task<CitizenUser?> GetByEmailAsync(string email);
    Task<CitizenUser?> GetByIdAsync(int id);
    Task AddAsync(CitizenUser user);
    Task UpdateAsync(CitizenUser user);
}
