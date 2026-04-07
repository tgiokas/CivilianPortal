using CitizenPortal.Application.Dtos.App;

namespace CitizenPortal.Application.Dtos.Auth;

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public CitizenUserDto? Citizen { get; set; }
}