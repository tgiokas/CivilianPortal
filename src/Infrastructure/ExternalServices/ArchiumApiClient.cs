using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Infrastructure.ApiClients;

namespace CitizenPortal.Infrastructure.ExternalServices;

/// Outbound HTTP client for ARCHIUM's Backend API.
public class ArchiumApiClient : ApiClientBase, IArchiumApiClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly KeycloakSettings _keycloakSettings;
    private readonly ArchiumClientSettings _archiumSettings;

    private string tokenEndpoint => $"{_keycloakSettings.BaseUrl}/realms/{_keycloakSettings.DmsRealm}/protocol/openid-connect/token";
    private const string foldersEndpoint = "/api/v1/folder/content";
    private const string createFolderEndpoint = "/api/v1/folder/ops";
    private const string uploadFileEndpoint = "/api/v1/file/temp";
    private const string downloadEndpoint = "/api/v1/file";
    private const string tempBucketName = "temp";
    private const string protocolEndpoint = "/api/v1/received-documents/ops";
    private const string archiveFilesEndpoint = "/api/v1/folder/ops";     

    public ArchiumApiClient(HttpClient httpClient, ILogger<ArchiumApiClient> logger, IOptions<KeycloakSettings> settings, IOptions<ArchiumClientSettings> archiumSettings)
        : base(httpClient, logger)
    {
        _keycloakSettings = settings.Value;
        _archiumSettings = archiumSettings.Value;
    }

    public async Task<FolderListResult?> GetFoldersAsync(long? parentId, CancellationToken cancellationToken = default)
    {
        var uri = parentId is null
            ? foldersEndpoint
            : $"{foldersEndpoint}?parentId={parentId}";

        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        var accessToken = await GetDmsAccessTokenAsync();

        request.Headers.Add("roleId", _archiumSettings.RoleId);
        request.Headers.Add("organizationUnitId", _archiumSettings.OrganizationUnitId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken?.Access_token);

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ARCHIUM returned {StatusCode} listing folders (parentId={ParentId})",
                (int)response.StatusCode, parentId);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<Result<FolderListResult>>(json, _jsonOptions);

        if (result is null || !result.Success || result.Data is null)
        {
            _logger.LogError(
                "ARCHIUM returned unsuccessful folder-listing response (parentId={ParentId}): {ErrorCode} {Message}",
                parentId, result?.ErrorCode, result?.Message);
            return null;
        }

        return result.Data;
    }

    public async Task<CreateFolderResult?> CreateFolderAsync(
        string subject, long? parentId, CancellationToken cancellationToken = default)
    {
        var payload = new CreateFolderRequest
        {
            Subject = subject,
            ParentId = parentId
        };

        var request = new HttpRequestMessage(HttpMethod.Post, createFolderEndpoint)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };

        var accessToken = await GetDmsAccessTokenAsync();

        request.Headers.Add("roleId", _archiumSettings.RoleId);
        request.Headers.Add("organizationUnitId", _archiumSettings.OrganizationUnitId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken?.Access_token);

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ARCHIUM returned {StatusCode} creating folder '{Subject}'",
                (int)response.StatusCode, subject);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<Result<CreateFolderResult>>(json, _jsonOptions);

        if (result is null || !result.Success || result.Data is null)
        {
            _logger.LogError(
                "ARCHIUM returned unsuccessful create-folder response for '{Subject}': {ErrorCode} {Message}",
                subject, result?.ErrorCode, result?.Message);
            return null;
        }

        return result.Data;
    }

    public async Task<DownloadedFileResult?> GetFileAsync(
        long fileId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{downloadEndpoint}/{fileId}");

        var accessToken = await GetDmsAccessTokenAsync();

        request.Headers.Add("roleId", _archiumSettings.RoleId);
        request.Headers.Add("organizationUnitId", _archiumSettings.OrganizationUnitId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken?.Access_token);

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ARCHIUM returned {StatusCode} retrieving file {FileId}",
                (int)response.StatusCode, fileId);
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;

        return new DownloadedFileResult
        {
            Content = bytes,
            ContentType = contentType,
            FileName = fileName?.Trim('"')
        };
    }

    public async Task<UploadDocumentResult?> UploadDocumentAsync(
        IFormFile file, string fileName, CancellationToken cancellationToken = default)
    {
        await using var fileStream = file.OpenReadStream();
        using var content = new MultipartFormDataContent
        {
            { new StringContent(tempBucketName), "bucketName" }
        };

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, uploadFileEndpoint)
        {
            Content = content
        };

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ARCHIUM returned {StatusCode} converting temp file '{FileName}'",
                (int)response.StatusCode, fileName);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<Result<UploadDocumentResult>>(json, _jsonOptions);

        if (result is null || !result.Success || result.Data is null)
        {
            _logger.LogError(
                "ARCHIUM returned unsuccessful temp conversion response for file '{FileName}': {ErrorCode} {Message}",
                fileName, result?.ErrorCode, result?.Message);
            return null;
        }

        return result.Data;
    }

    public async Task<ProtocolDocumentResult?> GetProtocolForDocumentAsync(
        long fileId, string subject, string externalIntegration, IReadOnlyCollection<long> accompanyingFileIds,
        CancellationToken cancellationToken = default)
    {
        var payload = new ProtocolDocumentRequest
        {
            FileId = fileId,
            Subject = subject,
            ExternalIntegration = externalIntegration,
            AccompaningFiles = accompanyingFileIds.ToList()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, protocolEndpoint)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };
        var accessToken = await GetDmsAccessTokenAsync();

        request.Headers.Add("roleId", _archiumSettings.RoleId);
        request.Headers.Add("organizationUnitId", _archiumSettings.OrganizationUnitId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken?.Access_token);

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "ARCHIUM returned {StatusCode} fetching protocol for document file {FileId} with {AttachmentCount} accompanying file(s)",
                (int)response.StatusCode, fileId, accompanyingFileIds.Count);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<Result<ProtocolDocumentResult>>(json, _jsonOptions);

        if (result is null || !result.Success || result.Data is null)
        {
            _logger.LogError(
                "ARCHIUM returned unsuccessful protocol response for document file {FileId}: {ErrorCode} {Message}",
                fileId, result?.ErrorCode, result?.Message);
            return null;
        }

        return result.Data;
    }

    public async Task<bool> ArchiveDocumentsToFolderAsync(
        long folderId, UpdateFolderRequest folderRequest, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{archiveFilesEndpoint}/{folderId}")
        {
            Content = JsonContent.Create(folderRequest, options: _jsonOptions)
        };

        var accessToken = await GetDmsAccessTokenAsync();

        request.Headers.Add("roleId", _archiumSettings.RoleId);
        request.Headers.Add("organizationUnitId", _archiumSettings.OrganizationUnitId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken?.Access_token);

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ARCHIUM returned {StatusCode} attaching {DocumentCount} document(s) to folder {FolderId}",
                (int)response.StatusCode, folderRequest.Documents.Count, folderId);
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<Result<object>>(json, _jsonOptions);

        if (result is null || !result.Success)
        {
            _logger.LogError(
                "ARCHIUM returned unsuccessful attach response for folder {FolderId}: {ErrorCode} {Message}",
                folderId, result?.ErrorCode, result?.Message);
            return false;
        }

        return true;
    }

    private async Task<TokenDto?> GetDmsAccessTokenAsync()
    {
        try
        {
            var content = new FormUrlEncodedContent(new []
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", _keycloakSettings.OpsDmsClientId),
                new KeyValuePair<string, string>("client_secret", _keycloakSettings.OpsDmsClientSecret),
                new KeyValuePair<string, string>("username", _keycloakSettings.OpsDmsUser),
                new KeyValuePair<string, string>("password", _keycloakSettings.OpsDmsPassword)
            });

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = content
            };

            var response = await SendRequestAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get Keycloak DMS token: {Status}", response.StatusCode);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var newToken = JsonSerializer.Deserialize<TokenDto>(jsonResponse, _jsonOptions);
            if (newToken == null)
            {
                _logger.LogWarning("Failed to deserialize Keycloak DMS token.");
                return null;
            }

            // TODO: Do we need to cache token here ?

            return newToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Keycloak DMS token.");
            return null;
        }
    }
}