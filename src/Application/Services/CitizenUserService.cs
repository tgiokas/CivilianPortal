using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Interfaces;

namespace CitizenPortal.Application.Services;

public class CitizenUserService : ICitizenUserService
{
    private readonly ICitizenUserRepository _citizenUserRepo;
    private readonly IApplicationDbContext _dbContext;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<CitizenUserService> _logger;

    public CitizenUserService(
        ICitizenUserRepository citizenUserRepo,
        IApplicationDbContext dbContext,
        IErrorCatalog errors,
        ILogger<CitizenUserService> logger)
    {
        _citizenUserRepo = citizenUserRepo;
        _dbContext = dbContext;
        _errors = errors;
        _logger = logger;
    }

    /// <summary>
    /// Called on first login — auto-provisions citizen from Keycloak JWT claims.
    /// If the citizen already exists, returns the existing record.
    /// </summary>
    public async Task<Result<CitizenUserDto>> GetOrCreateCitizenAsync(
        Guid keycloakUserId, string email, string? firstName = null, string? lastName = null)
    {
        var existing = await _citizenUserRepo.GetByKeycloakUserIdAsync(keycloakUserId);
        if (existing is not null)
        {
            return Result<CitizenUserDto>.Ok(MapToDto(existing));
        }

        try
        {
            var citizen = new CitizenUser
            {
                KeycloakUserId = keycloakUserId,
                Email = email,
                FirstName = firstName,
                LastName = lastName
            };

            await _citizenUserRepo.AddAsync(citizen);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Auto-provisioned citizen {Email} (Keycloak: {KeycloakUserId})", email, keycloakUserId);

            return Result<CitizenUserDto>.Ok(MapToDto(citizen));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create citizen user {Email}", email);
            return _errors.Fail<CitizenUserDto>(ErrorCodes.PORTAL.UserCreateFailed);
        }
    }

    public async Task<Result<CitizenUserDto>> GetCitizenByKeycloakIdAsync(Guid keycloakUserId)
    {
        var citizen = await _citizenUserRepo.GetByKeycloakUserIdAsync(keycloakUserId);
        if (citizen is null)
            return _errors.Fail<CitizenUserDto>(ErrorCodes.PORTAL.UserNotFound);

        return Result<CitizenUserDto>.Ok(MapToDto(citizen));
    }

    private static CitizenUserDto MapToDto(CitizenUser user) => new()
    {
        KeycloakUserId = user.KeycloakUserId,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName
    };
}
