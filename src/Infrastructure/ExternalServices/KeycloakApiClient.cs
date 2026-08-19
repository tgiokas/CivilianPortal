using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
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
    private readonly string _realm;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string tokenEndpoint => $"realms/{_realm}/protocol/openid-connect/token";
    private string logoutEndpoint => $"realms/{_realm}/protocol/openid-connect/logout";
    private string userEndpoint => $"/admin/realms/{_realm}/users";   

    public KeycloakApiClient(HttpClient httpClient, IOptions<KeycloakSettings> keycloakOptions, ILogger<KeycloakApiClient> logger)
        : base(httpClient, logger)
    {
        var settings = keycloakOptions.Value;

        _realm = settings.CitizenRealm;
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
        return JsonSerializer.Deserialize<TokenDto>(json, _jsonOptions);
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

    /// Get Access Token using Client Credentials grant.
    public async Task<TokenDto?> GetClientCredentialsTokenAsync(string clientId, string clientSecret)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
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
        return JsonSerializer.Deserialize<TokenDto>(jsonResponse, _jsonOptions);
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
        return JsonSerializer.Deserialize<TokenDto>(json, _jsonOptions);
    }

    /// Logout - revokes the refresh token in Keycloak.
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

    public async Task<KeycloakUser?> GetUserByNameAsync(string username)
    {
        var requestUrl = $"{userEndpoint}/?exact=true&username={username}";
        var request = await CreateAuthenticatedRequestAsync(HttpMethod.Get, requestUrl);

        var response = await SendRequestAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var users = JsonSerializer.Deserialize<List<KeycloakUser>>(jsonResponse);
        return users?.FirstOrDefault();
    }

    public async Task<KeycloakUser?> CreateUserAsync(string username, string password)
    {
        var newUser = new KeycloakUser
        {
            UserName = username,
            Email = username + "@testcbs.gr",
            FirstName = "firstName-" + username,
            LastName = "lastName-" + username,
            Enabled = true,
            EmailVerified = true,
            Credentials = new[]
            {
                new KeycloakCredential { Type = "password", Value =password, Temporary = false }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(newUser);
        
        var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, userEndpoint, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));

        var response = await SendRequestAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create user in Keycloak: {Response}", error);
            return null;
        }

        // Keycloak returns 201 Created, with a 'Location' header of the form:
        // http://keycloak-host/admin/realms/<realm>/users/<userId>
        var locationHeader = response.Headers.Location;

        if (locationHeader != null)
        {
            // Extract the ID from the final segment:
            var locationSegments = locationHeader.AbsolutePath.Split('/');
            string? createdUserId = locationSegments.LastOrDefault() ?? string.Empty;
            newUser.Id = createdUserId;

            return newUser;
        }

        return null;
    }

    protected async Task<TokenDto?> GetAdminAccessTokenAsync()
    {
        try
        {
            var newToken = await GetClientCredentialsTokenAsync(_clientId, _clientSecret);
            if (newToken == null)
            {
                _logger.LogWarning("Failed to deserialize Keycloak admin token.");
                return null;
            }           

            return newToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Keycloak admin token.");
            return null;
        }
    }

    protected async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(HttpMethod method, string requestUrl, HttpContent? content = null)
    {
        var adminToken = await GetAdminAccessTokenAsync();
        var request = new HttpRequestMessage(method, requestUrl)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken?.Access_token);
        return request;
    }
}
