using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Interfaces;

namespace CitizenPortal.Application.Services;

public class UserService : IUserService
{
    private readonly ICitizenUserRepository _citizenUserRepo;
    private readonly IErrorCatalog _errors;

    public UserService(ICitizenUserRepository citizenUserRepo, IErrorCatalog errors)
    {
        _citizenUserRepo = citizenUserRepo;
        _errors = errors;
    }

    public async Task<Result<CitizenUserDto>> GetUserProfileAsync(Guid userId)
    {
        var user = await _citizenUserRepo.GetByKeycloakUserIdReadOnlyAsync(userId);

        if (user is null)
            return _errors.Fail<CitizenUserDto>(ErrorCodes.PORTAL.UserNotFound);

        return Result<CitizenUserDto>.Ok(new CitizenUserDto
        {
            KeycloakUserId = user.KeycloakUserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FatherName = user.FatherName,
            VatId = user.VatId,
            LegalEntity = user.LegalEntity
        });
    }
}
