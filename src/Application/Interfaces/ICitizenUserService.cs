using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

public interface ICitizenUserService
{
    Task<Result<CitizenUserDto>> GetOrCreateCitizenAsync(Guid keycloakUserId, string email, string? firstName = null, string? lastName = null);
    Task<Result<CitizenUserDto>> GetCitizenByKeycloakIdAsync(Guid keycloakUserId);
}
