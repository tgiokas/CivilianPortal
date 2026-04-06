namespace CitizenPortal.Application.Dtos;

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public CitizenUserDto? Citizen { get; set; }
}

public class RefreshResponseDto
{
    public string Access_token { get; set; } = string.Empty;
    public string Refresh_token { get; set; } = string.Empty;
    public int Expires_in { get; set; }
}

public class TokenDto
{
    public string? Access_token { get; set; }
    public string? Refresh_token { get; set; }
    public int Expires_in { get; set; }
    public string? Token_type { get; set; }
}
