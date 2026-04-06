using CitizenPortal.Application.Dtos.Auth;

namespace CitizenPortal.Application.Interfaces;

public interface IKeycloakClientAuthentication
{
    Task<TokenDto?> GetAccessTokenByCodeAsync(string code);
    Task<TokenDto?> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(string refreshToken);
}
