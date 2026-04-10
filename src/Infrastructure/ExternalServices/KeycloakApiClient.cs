using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Infrastructure.ApiClients;

namespace CitizenPortal.Infrastructure.ExternalServices;

/// Keycloak authentication client for CitizenPortal.
/// Handles Authorization Code, Refresh, and Logout flows for citizen users.
public class KeycloakApiClient : ApiClientBase, IKeycloakApiClient
{
    private readonly string _keycloakServerUrl;
    private readonly string _realm;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string tokenEndpoint
       => $"/realms/{_realm}/protocol/openid-connect/token";

    private string logoutEndpoint
        => $"/realms/{_realm}/protocol/openid-connect/logout";

    public KeycloakApiClient(HttpClient httpClient, IOptions<KeycloakSettings> keycloakOptions, ILogger<KeycloakApiClient> logger)
        : base(httpClient, logger)
    {
        var settings = keycloakOptions.Value;

        _keycloakServerUrl = settings.BaseUrl;
        _realm = settings.Realm;
        _clientId = settings.ClientId;
        _clientSecret = settings.ClientSecret;
        _redirectUri = settings.RedirectUri;
    }   

    /// Get Access Token using authorization code (Authorization Code Grant).
    /// Called after GSIS/TaxisNet redirects back with a code.
    public async Task<TokenDto?> GetAccessTokenByCodeAsync(string code)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = _redirectUri
        };

        var content = new FormUrlEncodedContent(parameters);
        var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = content
        };

        var response = await SendRequestAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenDto>(json, JsonOptions);
    }

    /// Simple login via Keycloak Direct Access Grant (Resource Owner Password Credentials).
    /// Requires "Direct Access Grants" enabled on the Keycloak client.
    public async Task<TokenDto?> GetUserAccessTokenAsync(string username, string password)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "password",           
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,           
            ["username"] = username,
            ["password"] = password
        };

        var content = new FormUrlEncodedContent(parameters);
        var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = content
        };

        var response = await SendRequestAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenDto>(jsonResponse);
    }

    /// Refresh the access token using the refresh token.
    public async Task<TokenDto?> RefreshTokenAsync(string refreshToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["refresh_token"] = refreshToken
        };

        var content = new FormUrlEncodedContent(parameters);
        var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = content
        };

        var response = await SendRequestAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenDto>(json, JsonOptions);
    }

    /// Logout — revokes the refresh token in Keycloak.
    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["refresh_token"] = refreshToken
        };

        var content = new FormUrlEncodedContent(parameters);
        var request = new HttpRequestMessage(HttpMethod.Post, logoutEndpoint)
        {
            Content = content
        };

        var response = await SendRequestAsync(request);
        return response.IsSuccessStatusCode;
    }
}