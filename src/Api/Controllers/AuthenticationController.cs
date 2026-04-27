using Microsoft.AspNetCore.Mvc;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IConfiguration _configuration;

    public AuthenticationController(IAuthenticationService authenticationService, IConfiguration configuration)
    {
        _authenticationService = authenticationService;
        _configuration = configuration;
    }

    /// Simple username/password login via Keycloak Direct Access Grant.
    /// POST /authentication/login
    /// Will Not be deployed to production (For Testing Only)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(Result<string>.Fail("Username and password are required."));
        }

        var result = await _authenticationService.LoginAsync(request);
        if (result == null || !result.Success)
        {
            return Accepted(result);
        }
        
        if (!string.IsNullOrWhiteSpace(result.Data?.RefreshToken))
        {
            AppendRefreshTokenCookie(result.Data.RefreshToken);
        }

        return Ok(result);
    }

    /// OAuth2 callback — GSIS/TaxisNet redirects here after citizen authenticates.
    /// Exchanges the authorization code for tokens and auto-provisions the citizen
    /// in our DB if they don't exist yet (same pattern as DMS.Auth).
    /// Flow: GSIS login → Keycloak CitizenRealm → redirect with code → this endpoint
    [HttpGet("oauth2callback")]
    public async Task<IActionResult> OAuth2Callback()
    {
        var query = Request.Query;
        var code = query["code"].ToString();

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(Result<string>.Fail("Authorization code is required."));
        }

        var frontendRedirectUrl = _configuration["FRONTEND_REDIRECTURI"]
            ?? "http://localhost:3000";

        var result = await _authenticationService.OAuth2CallbackAsync(code);
        
        if (!string.IsNullOrEmpty(result?.Data?.AccessToken) &&
            !string.IsNullOrEmpty(result?.Data?.RefreshToken))
        {
            AppendRefreshTokenCookie(result.Data.RefreshToken);
        }

        return Redirect(frontendRedirectUrl);
    }

   
    /// Refresh the access token using the refresh token.   
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshTokenValue = ExtractRefreshTokenFromCookie(Request.HttpContext);
        if (string.IsNullOrEmpty(refreshTokenValue))
        {
            return Accepted(new { message = "Refresh token is missing" });
        }

        var result = await _authenticationService.RefreshTokenAsync(refreshTokenValue);
        if (!result.Success)
        {
            return Accepted(result);
        }

        if (!string.IsNullOrWhiteSpace(result.Data?.Refresh_token))
        {
            AppendRefreshTokenCookie(result.Data.Refresh_token);
        }

        return Ok(result);
    }

    /// Logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshTokenValue = ExtractRefreshTokenFromCookie(Request.HttpContext);
        if (string.IsNullOrEmpty(refreshTokenValue))
        {
            return Accepted(new { message = "Refresh token is missing" });
        }

        var result = await _authenticationService.LogoutAsync(refreshTokenValue);
        if (!result.Success)
        {
            return Accepted(result);
        }

        Response.Cookies.Delete("refresh_token");

        return Ok(result);
    }

    private static string? ExtractRefreshTokenFromCookie(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.TryGetValue("refresh_token", out var token))
        {
            return token;
        }

        return null;
    }

    private void AppendRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromDays(7)
        });
    }
}