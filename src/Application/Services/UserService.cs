using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Interfaces;

namespace CitizenPortal.Application.Services;

public class UserService : IUserService
{
    private readonly ICitizenUserRepository _citizenUserRepo;

    public UserService(ICitizenUserRepository citizenUserRepo)
    {
        _citizenUserRepo = citizenUserRepo;
    }

    public async Task<Result<CitizenUserDto>> GetUserProfileAsync(Guid keycloakUserId)
    {
        var user = await _citizenUserRepo.GetByKeycloakUserIdReadOnlyAsync(keycloakUserId);

        if (user is null)
            return Result<CitizenUserDto>.Fail("User not found.");

        return Result<CitizenUserDto>.Ok(new CitizenUserDto
        {
            KeycloakUserId = user.KeycloakUserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }
}
