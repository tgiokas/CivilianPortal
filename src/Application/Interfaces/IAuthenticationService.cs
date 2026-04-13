using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

public interface IAuthenticationService
{
    Task<Result<LoginResponseDto>> OAuth2CallbackAsync(string code);
    Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request);
    Task<Result<RefreshResponseDto>> RefreshTokenAsync(string refreshToken);
    Task<Result<bool>> LogoutAsync(string refreshToken);
}
