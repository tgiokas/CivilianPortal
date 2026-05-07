using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

public interface IUserService
{
    Task<Result<CitizenUserDto>> GetUserProfileAsync(Guid keycloakUserId);
}
