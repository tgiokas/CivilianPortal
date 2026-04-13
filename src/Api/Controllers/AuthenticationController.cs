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

        // Set refresh token as HttpOnly cookie (same pattern as OAuth2Callback)
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
    public async Task<IActionResult> OAuth2Callback([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(Result<string>.Fail("Authorization code is required."));
        }

        var frontendRedirectUrl = _configuration["FRONTEND_REDIRECTURI"]
            ?? "http://localhost:3000";

        var result = await _authenticationService.OAuth2CallbackAsync(code);
        if (result == null || !result.Success)
        {
            return Accepted(result);
        }

        // Append refresh token as HttpOnly cookie (same as DMS.Auth)
        if (!string.IsNullOrWhiteSpace(result.Data?.RefreshToken))
        {
            AppendRefreshTokenCookie(result.Data.RefreshToken);
        }

        return Ok(result);
    }

    /// <summary>
    /// Refresh the access token using the refresh token.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
    {
        var refreshToken = request.RefreshToken;

        // Fallback: try reading from HttpOnly cookie
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            refreshToken = Request.Cookies["refresh_token"];
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(Result<string>.Fail("Refresh token is required."));
        }

        var result = await _authenticationService.RefreshTokenAsync(refreshToken);
        if (!result.Success)
        {
            return Accepted(result);
        }

        // Update the cookie with new refresh token
        if (!string.IsNullOrWhiteSpace(result.Data?.Refresh_token))
        {
            AppendRefreshTokenCookie(result.Data.Refresh_token);
        }

        return Ok(result);
    }

    /// <summary>
    /// Logout — revokes the refresh token in Keycloak.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto? request)
    {
        var refreshToken = request?.RefreshToken;

        // Fallback: try reading from HttpOnly cookie
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            refreshToken = Request.Cookies["refresh_token"];
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(Result<string>.Fail("Refresh token is required."));
        }

        var result = await _authenticationService.LogoutAsync(refreshToken);

        // Clear the refresh cookie
        Response.Cookies.Delete("refresh_token");

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
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