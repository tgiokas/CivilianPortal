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
    /// The portal server's own host name, resolved once for the process lifetime
    /// (it never changes) and recorded as the «όνομα εξυπηρετητή» in every audit row.
    private static readonly string ServerHostName = ResolveServerHostName();

    private readonly IKeycloakApiClient _keycloakClientAuth;
    private readonly ICitizenUserRepository _citizenUserRepo;
    private readonly IAuthenticationAuditLogRepository _auditRepo;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IKeycloakApiClient keycloakClientAuth,
        ICitizenUserRepository citizenUserRepo,
        IAuthenticationAuditLogRepository auditRepo,
        IErrorCatalog errors,
        ILogger<AuthenticationService> logger)
    {
        _keycloakClientAuth = keycloakClientAuth;
        _citizenUserRepo = citizenUserRepo;
        _auditRepo = auditRepo;
        _errors = errors;
        _logger = logger;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var tokenResponse = await _keycloakClientAuth.GetUserAccessTokenAsync(
            request.Username, request.Password);

        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.Access_token))
        {
            return _errors.Fail<LoginResponseDto>(ErrorCodes.PORTAL.AuthenticationFailed);
        }

        // Parse JWT claims and auto-provision citizen in Citizen-Portal DB
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(tokenResponse.Access_token);
        return await ProcessTokenAndProvisionCitizen(tokenResponse, jwtToken);
    }

    /// OAuth2 callback handler
    /// 1. Exchange authorization code for tokens via Keycloak
    /// 2. Parse JWT claims (sub, email, name, taxisnet_id)
    /// 3. Check if citizen exists in our DB
    /// 4. If not -> auto-provision (create CitizenUser)
    /// 5. Return tokens + citizen info
    public async Task<Result<LoginResponseDto>> OAuth2CallbackAsync(string code, AuthAuditContext auditContext)
    {
        // 1. Exchange code for tokens
        var tokenResponse = await _keycloakClientAuth.GetAccessTokenByCodeAsync(code);

        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.Access_token))
        {
            // No token -> we cannot know the provider or the user, but the call still happened.
            await WriteAuditAsync(auditContext, AuthenticationAuditLog.ProviderUnknown,
                username: null, keycloakUserId: null,
                success: false, failureReason: "Token exchange with Keycloak failed");

            return _errors.Fail<LoginResponseDto>(ErrorCodes.PORTAL.AuthenticationFailed);
        }

        // 2. Parse the access token once: reused for both auditing and citizen provisioning.
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(tokenResponse.Access_token);

        var provider = MapProvider(jwtToken.Claims.FirstOrDefault(c => c.Type == "identity_provider")?.Value);
        var username = jwtToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
                       ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "taxid" || c.Type == "afm")?.Value
                       ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        Guid? keycloakUserId =
            Guid.TryParse(jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value, out var parsedId)
                ? parsedId
                : null;

        // 3. Provision citizen in Citizen-Portal DB
        var result = await ProcessTokenAndProvisionCitizen(tokenResponse, jwtToken);

        // 4. Audit the outcome (success or failure) of this authentication call.
        await WriteAuditAsync(auditContext, provider, username, keycloakUserId,
            success: result.Success,
            failureReason: result.Success ? null : (result.Message ?? "Authentication failed"));

        return result;
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

    /// Private: shared logic for reading citizen claims + auto-provisioning.
    /// The caller supplies the already-parsed access token (parsed once at the call site).
    /// Used by both LoginAsync (password grant) and OAuth2CallbackAsync (code grant)
    private async Task<Result<LoginResponseDto>> ProcessTokenAndProvisionCitizen(TokenDto tokenResponse, JwtSecurityToken jwtToken)
    {
        var keycloakUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        var firstName = jwtToken.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
        var lastName = jwtToken.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value;
        var taxisNetId = jwtToken.Claims.FirstOrDefault(c => c.Type == "taxid" || c.Type == "afm")?.Value;
        var fatherName = jwtToken.Claims.FirstOrDefault(c => c.Type == "fathername")?.Value;

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

        bool legalEntity = false;
        if (string.IsNullOrWhiteSpace(firstName) || firstName == "null")
        {
            legalEntity = true;
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
                FatherName = fatherName,
                VatId = taxisNetId,
                LegalEntity = legalEntity
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
            if (!string.IsNullOrWhiteSpace(taxisNetId) && dbCitizen.VatId != taxisNetId)
            {
                dbCitizen.VatId = taxisNetId;
                updated = true;
            }
            if (!string.IsNullOrWhiteSpace(fatherName) && dbCitizen.FatherName != fatherName)
            {
                dbCitizen.FatherName = fatherName;
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
                LastName = dbCitizen.LastName,
                FatherName = dbCitizen.FatherName,
                VatId = dbCitizen.VatId
            }
        });
    }

    /// Best-effort resolution of the host's machine name. Environment.MachineName is
    /// cheap and effectively never throws, but we guard it so a hosting quirk can never
    /// break authentication; it is resolved once and cached in <see cref="ServerHostName"/>.
    private static string ResolveServerHostName()
    {
        try
        {
            return Environment.MachineName;
        }
        catch
        {
            return "Unknown";
        }
    }

    /// Resolve the «όνομα εξυπηρετητή / machine name» recorded in the audit row. Prefers the
    /// workstation name self-reported by the caller (X-Machine-Name header); falls back to the
    /// server host name when the header is absent so the column is never empty. The reported
    /// value is untrusted client input, so it is capped at the column length (256) to guarantee
    /// the audit row still persists instead of the insert throwing and the whole row being lost.
    private static string ResolveMachineName(string? reportedMachineName)
    {
        if (string.IsNullOrWhiteSpace(reportedMachineName))
            return ServerHostName;

        var trimmed = reportedMachineName.Trim();
        return trimmed.Length <= 256 ? trimmed : trimmed[..256];
    }

    /// Classify the Keycloak broker alias (identity_provider claim) into the audited
    /// authentication service. Matches on substring so it is resilient to the staging
    /// vs production alias variants (e.g. gsis-taxis-test / gsis-govuser-test).
    private static string MapProvider(string? idpAlias)
    {
        if (string.IsNullOrWhiteSpace(idpAlias))
            return AuthenticationAuditLog.ProviderUnknown;

        if (idpAlias.Contains("taxis", StringComparison.OrdinalIgnoreCase))
            return AuthenticationAuditLog.ProviderTaxisNet;

        if (idpAlias.Contains("gov", StringComparison.OrdinalIgnoreCase))
            return AuthenticationAuditLog.ProviderPublicAdministration;

        return AuthenticationAuditLog.ProviderUnknown;
    }

    /// Persist an audit record for an authentication call. Best-effort: a failure to
    /// write the audit log must never break the authentication flow itself.
    private async Task WriteAuditAsync(
        AuthAuditContext auditContext,
        string provider,
        string? username,
        Guid? keycloakUserId,
        bool success,
        string? failureReason)
    {
        try
        {
            await _auditRepo.AddAsync(new AuthenticationAuditLog
            {
                Provider = provider,
                Reason = AuthenticationAuditLog.DefaultReason,
                IpAddress = auditContext.IpAddress,
                MachineName = ResolveMachineName(auditContext.MachineName),
                Username = username,
                KeycloakUserId = keycloakUserId,
                Success = success,
                FailureReason = failureReason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write authentication audit log entry");
        }
    }
}