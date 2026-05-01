using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Interfaces;

namespace CitizenPortal.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IKeycloakApiClient _keycloakClientAuth;
    private readonly ICitizenUserRepository _citizenUserRepo;    
    private readonly IErrorCatalog _errors;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IKeycloakApiClient keycloakClientAuth,
        ICitizenUserRepository citizenUserRepo,        
        IErrorCatalog errors,
        ILogger<AuthenticationService> logger)
    {
        _keycloakClientAuth = keycloakClientAuth;
        _citizenUserRepo = citizenUserRepo;        
        _errors = errors;
        _logger = logger;
    }

    /// Username/password login via Keycloak Direct Access Grant.
    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var tokenResponse = await _keycloakClientAuth.GetUserAccessTokenAsync(
            request.Username, request.Password);

        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.Access_token))
        {
            return _errors.Fail<LoginResponseDto>(ErrorCodes.PORTAL.AuthenticationFailed);
        }

        // Parse JWT claims and auto-provision citizen in Citizen-Portal DB
        return await ProcessTokenAndProvisionCitizen(tokenResponse);
    }

    /// OAuth2 callback handler
    /// 1. Exchange authorization code for tokens via Keycloak
    /// 2. Parse JWT claims (sub, email, name, taxisnet_id)
    /// 3. Check if citizen exists in our DB
    /// 4. If not → auto-provision (create CitizenUser)
    /// 5. Return tokens + citizen info
    public async Task<Result<LoginResponseDto>> OAuth2CallbackAsync(string code)
    {
        // 1. Exchange code for tokens
        var tokenResponse = await _keycloakClientAuth.GetAccessTokenByCodeAsync(code);
        
        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.Access_token))
        {
            return _errors.Fail<LoginResponseDto>(ErrorCodes.PORTAL.AuthenticationFailed);
        }

        // 2. Parse claims and provision citizen in Citizen-Portal DB
        return await ProcessTokenAndProvisionCitizen(tokenResponse);
    }

    public async Task<Result<RefreshResponseDto>> RefreshTokenAsync(string refreshToken)
    {
        var tokenResponse = await _keycloakClientAuth.RefreshTokenAsync(refreshToken);
        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.Access_token))
        {
            return _errors.Fail<RefreshResponseDto>(ErrorCodes.PORTAL.RefreshFailed);
        }

        return Result<RefreshResponseDto>.Ok(new RefreshResponseDto
        {
            Access_token = tokenResponse.Access_token,
            Refresh_token = tokenResponse.Refresh_token ?? string.Empty,
            Expires_in = tokenResponse.Expires_in ?? 0
        });
    }

    public async Task<Result<bool>> LogoutAsync(string refreshToken)
    {
        var result = await _keycloakClientAuth.LogoutAsync(refreshToken);
        if (!result)
        {
            return _errors.Fail<bool>(ErrorCodes.PORTAL.LogoutFailed);
        }

        return Result<bool>.Ok(true, message: "Logout successful");
    }

    // Private: shared logic for JWT parsing + citizen auto-provisioning
    // Used by both LoginAsync (password grant) and OAuth2CallbackAsync (code grant)
    private async Task<Result<LoginResponseDto>> ProcessTokenAndProvisionCitizen(TokenDto tokenResponse)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenResponse.Access_token);

        var keycloakUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        var firstName = jwtToken.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
        var lastName = jwtToken.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value;
        var taxisNetId = jwtToken.Claims.FirstOrDefault(c => c.Type == "taxid" || c.Type == "afm")?.Value;

        if (string.IsNullOrWhiteSpace(keycloakUserId) || string.IsNullOrWhiteSpace(email))
        {
            _logger.LogError("JWT token missing sub or email claims");
            return _errors.Fail<LoginResponseDto>(ErrorCodes.PORTAL.AuthenticationFailed);
        }

        if (!Guid.TryParse(keycloakUserId, out var userId))
        {
            _logger.LogError("Invalid Keycloak user ID format: {Id}", keycloakUserId);
            return _errors.Fail<LoginResponseDto>(ErrorCodes.PORTAL.AuthenticationFailed);
        }

        // 3. Check if citizen exists in Citizen-portal DB
        var dbCitizen = await _citizenUserRepo.GetByKeycloakUserIdAsync(userId);

        // 4. If not exists --> auto-provision (GetOrCreateAsync handles concurrent first-logins)
        if (dbCitizen == null)
        {
            _logger.LogInformation("New citizen login. Provisioning user {Email} (Keycloak: {UserId})", email, userId);

            var candidate = new CitizenUser
            {
                KeycloakUserId = userId,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                TaxisNetId = taxisNetId
            };

            var (provisioned, created) = await _citizenUserRepo.GetOrCreateAsync(candidate);
            dbCitizen = provisioned;

            if (created)
                _logger.LogInformation("Citizen {Email} provisioned successfully (Id: {Id})", email, dbCitizen.Id);
            else
                _logger.LogWarning("Concurrent provisioning detected for {Email} (Keycloak: {UserId}); using existing record.", email, userId);
        }
        else
        {
            bool updated = false;

            if (!string.IsNullOrWhiteSpace(firstName) && dbCitizen.FirstName != firstName)
            {
                dbCitizen.FirstName = firstName;
                updated = true;
            }
            if (!string.IsNullOrWhiteSpace(lastName) && dbCitizen.LastName != lastName)
            {
                dbCitizen.LastName = lastName;
                updated = true;
            }
            if (!string.IsNullOrWhiteSpace(taxisNetId) && dbCitizen.TaxisNetId != taxisNetId)
            {
                dbCitizen.TaxisNetId = taxisNetId;
                updated = true;
            }

            if (updated)
            {
                await _citizenUserRepo.UpdateAsync(dbCitizen);
                _logger.LogInformation("Updated citizen {Email} profile from GSIS claims", email);
            }

        }

        // 5. Return tokens + citizen info
        return Result<LoginResponseDto>.Ok(new LoginResponseDto
        {
            AccessToken = tokenResponse.Access_token ?? string.Empty,
            RefreshToken = tokenResponse.Refresh_token ?? string.Empty,
            ExpiresIn = tokenResponse.Expires_in ?? 0,
            Citizen = new CitizenUserDto
            {
                KeycloakUserId = dbCitizen.KeycloakUserId,
                Email = dbCitizen.Email,
                FirstName = dbCitizen.FirstName,
                LastName = dbCitizen.LastName
            }
        });
    }
}