using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Infrastructure.ApiClients;
using System.Text;

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

    public async Task<string?> CreateUserInKeycloakAsync(string username, string email, string password,
        string? firstName = null, string? lastName = null)
    {
        // 1. Get admin token via client_credentials
        var adminToken = await GetAdminAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(adminToken))
        {
            _logger.LogError("Failed to obtain Keycloak admin token for user creation");
            return null;
        }

        // 2. Build the Keycloak user representation
        var userPayload = new
        {
            username = username,
            email = email,
            firstName = firstName ?? string.Empty,
            lastName = lastName ?? string.Empty,
            enabled = true,
            emailVerified = true,
            credentials = new[]
            {
                new { type = "password", value = password, temporary = false }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(userPayload);
        var requestUrl = $"{_keycloakServerUrl}/admin/realms/{_realm}/users";

        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var response = await SendRequestAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create user in Keycloak: {StatusCode} {Body}",
                (int)response.StatusCode, error);
            return null;
        }

        // Keycloak returns 201 with Location header: .../admin/realms/{realm}/users/{userId}
        var locationHeader = response.Headers.Location;
        if (locationHeader != null)
        {
            var segments = locationHeader.AbsolutePath.Split('/');
            var createdUserId = segments.LastOrDefault();
            _logger.LogInformation("Created Keycloak user {Username} with ID {UserId}",
                username, createdUserId);
            return createdUserId;
        }

        _logger.LogWarning("User created but no Location header returned");
        return null;
    }

    private async Task<string?> GetAdminAccessTokenAsync()
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
        });

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_keycloakServerUrl}/realms/{_realm}/protocol/openid-connect/token")
        {
            Content = content
        };

        var response = await SendRequestAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to get Keycloak admin token: {Status}", response.StatusCode);
            return null;
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<TokenDto>(jsonResponse);
        return token?.Access_token;
    }
}
