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

    private const string FoldersEndpoint = "/api/v1/external-portal/folders";
    private const string FilesEndpoint = "/api/v1/file/tempAndConvert";
    private const string TempBucketName = "temp";
    private const string ReceivedDocumentsOpsEndpoint = "/api/v1/received-documents/ops";

    private readonly ArchiumClientSettings _settings;

    public ArchiumApiClient(HttpClient httpClient, ILogger<ArchiumApiClient> logger, IOptions<ArchiumClientSettings> settings)
        : base(httpClient, logger)
    {
        _settings = settings.Value;
    }

    public async Task<FolderListResult?> GetFoldersAsync(long? parentId, CancellationToken cancellationToken = default)
    {
        var uri = parentId is null
            ? FoldersEndpoint
            : $"{FoldersEndpoint}?parentId={parentId}";

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ARCHIUM returned {StatusCode} listing folders (parentId={ParentId})",
                (int)response.StatusCode, parentId);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<FolderListResult>(json, _jsonOptions);
    }

    public async Task<CreateFolderResult?> CreateFolderAsync(
        long? parentFolderId, string folderName, string folderCategory,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(folderName), "folderName" },
            { new StringContent(folderCategory), "folderCategory" }
        };

        if (parentFolderId is not null)
            content.Add(new StringContent(parentFolderId.Value.ToString()), "parentFolderId");

        var request = new HttpRequestMessage(HttpMethod.Post, FoldersEndpoint)
        {
            Content = content
        };

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ARCHIUM returned {StatusCode} creating folder '{FolderName}'",
                (int)response.StatusCode, folderName);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<CreateFolderResult>(json, _jsonOptions);
    }

    public async Task<RetrievedFileResult?> GetFileAsync(
        long fileId, string callerSystemId, CancellationToken cancellationToken = default)
    {
        var uri = $"{FilesEndpoint}/{fileId}?callerSystemId={Uri.EscapeDataString(callerSystemId)}";

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ARCHIUM returned {StatusCode} retrieving file {FileId}",
                (int)response.StatusCode, fileId);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<RetrievedFileResult>(json, _jsonOptions);
    }

    public async Task<UploadDocumentResult?> UploadDocumentAsync(
        IFormFile file, string fileName, CancellationToken cancellationToken = default)
    {
        await using var fileStream = file.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);
        return await UploadDocumentAsync(
            streamContent,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            fileName,
            cancellationToken);
    }

    public async Task<UploadDocumentResult?> UploadDocumentAsync(
        byte[] content, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
        using var byteArrayContent = new ByteArrayContent(content);
        return await UploadDocumentAsync(
            byteArrayContent,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            fileName,
            cancellationToken);
    }

    private async Task<UploadDocumentResult?> UploadDocumentAsync(
        HttpContent fileContent, string contentType, string fileName, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(TempBucketName), "bucketName" }
        };

        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, FilesEndpoint)
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

        var request = new HttpRequestMessage(HttpMethod.Post, ReceivedDocumentsOpsEndpoint)
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

    public async Task<bool> AttachDocumentsToFolderAsync(
        long folderId, UpdateFolderDocumentsRequest folderRequest, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/folder/{folderId}")
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