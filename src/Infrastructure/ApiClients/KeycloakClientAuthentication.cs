using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Infrastructure.ApiClients;

public class KeycloakClientAuthentication : IKeycloakClientAuthentication
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KeycloakClientAuthentication> _logger;
    private readonly string _tokenEndpoint;
    private readonly string _logoutEndpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    public KeycloakClientAuthentication(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<KeycloakClientAuthentication> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var keycloakUrl = configuration["KEYCLOAK_BASEURL"] ?? "http://keycloak:8080";
        var realm = configuration["KEYCLOAK_REALM"] ?? "CitizenRealm";
        _clientId = configuration["KEYCLOAK_CLIENTID"] ?? "citizen-portal-app";
        _clientSecret = configuration["KEYCLOAK_CLIENTSECRET"] ?? "";
        _redirectUri = configuration["KEYCLOAK_REDIRECTURI"] ?? "http://localhost:3000/oauth2callback";

        _tokenEndpoint = $"{keycloakUrl}/realms/{realm}/protocol/openid-connect/token";
        _logoutEndpoint = $"{keycloakUrl}/realms/{realm}/protocol/openid-connect/logout";
    }

    /// <summary>
    /// Exchange authorization code for tokens (Authorization Code Flow).
    /// Called after GSIS/TaxisNet redirects back with a code.
    /// </summary>
    public async Task<TokenDto?> GetAccessTokenByCodeAsync(string code)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = _clientId,
                ["redirect_uri"] = _redirectUri
            };

            if (!string.IsNullOrWhiteSpace(_clientSecret))
                parameters["client_secret"] = _clientSecret;

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(_tokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token exchange failed ({StatusCode}): {Error}", response.StatusCode, error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TokenDto>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging authorization code for token");
            return null;
        }
    }

    public async Task<TokenDto?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _clientId
            };

            if (!string.IsNullOrWhiteSpace(_clientSecret))
                parameters["client_secret"] = _clientSecret;

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(_tokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token refresh failed ({StatusCode}): {Error}", response.StatusCode, error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TokenDto>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return null;
        }
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["refresh_token"] = refreshToken
            };

            if (!string.IsNullOrWhiteSpace(_clientSecret))
                parameters["client_secret"] = _clientSecret;

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(_logoutEndpoint, content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return false;
        }
    }
}
