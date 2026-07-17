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

    private const string foldersEndpoint = "/api/v1/folder/content/ops";
    private const string createFolderEndpoint = "/api/v1/folder/ops";
    private const string uploadFileEndpoint = "/api/v1/file/temp";
    private const string downloadEndpoint = "/api/v1/file";
    private const string tempBucketName = "temp";
    private const string protocolEndpoint = "/api/v1/received-documents/ops";
    private const string archiveFilesEndpoint = "/api/v1/folder";

    private readonly ArchiumClientSettings _settings;

    public ArchiumApiClient(HttpClient httpClient, ILogger<ArchiumApiClient> logger, IOptions<ArchiumClientSettings> settings)
        : base(httpClient, logger)
    {
        _settings = settings.Value;
    }

    public async Task<FolderListResult?> GetFoldersAsync(long? parentId, CancellationToken cancellationToken = default)
    {
        var uri = parentId is null
            ? foldersEndpoint
            : $"{foldersEndpoint}?parentId={parentId}";

        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        request.Headers.Add("roleId", _settings.RoleId);
        request.Headers.Add("organizationUnitId", _settings.OrganizationUnitId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AuthToken);
        request.Headers.Add("Cookie", _settings.Cookie);

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

        request.Headers.Add("roleId", _settings.RoleId);
        request.Headers.Add("organizationUnitId", _settings.OrganizationUnitId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AuthToken);

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

        request.Headers.Add("roleId", _settings.RoleId);
        request.Headers.Add("organizationUnitId", _settings.OrganizationUnitId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AuthToken);

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

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "ARCHIUM returned {StatusCode} fetching protocol for document file {FileId} with {AttachmentCount} accompanying file(s)",
                (int)response.StatusCode, fileId, accompanyingFileIds.Count);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<ProtocolDocumentResult>(json, _jsonOptions);
    }

    public async Task<bool> ArchiveDocumentsToFolderAsync(
        long folderId, UpdateFolderRequest folderRequest, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{archiveFilesEndpoint}/{folderId}")
        {
            Content = JsonContent.Create(folderRequest, options: _jsonOptions)
        };

        request.Headers.Add("roleId", _settings.RoleId);
        request.Headers.Add("organizationUnitId", _settings.OrganizationUnitId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AuthToken);
        request.Headers.Add("Cookie", _settings.Cookie);

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "ARCHIUM returned {StatusCode} attaching {DocumentCount} document(s) to folder {FolderId}",
                (int)response.StatusCode, folderRequest.Documents.Count, folderId);
            return false;
        }

        return true;
    }
}