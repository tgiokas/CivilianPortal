using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

public interface IKeycloakApiClient
{
    Task<TokenDto?> GetAccessTokenByCodeAsync(string code);
    Task<TokenDto?> GetUserAccessTokenAsync(string username, string password);
    Task<TokenDto?> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(string refreshToken);
}
