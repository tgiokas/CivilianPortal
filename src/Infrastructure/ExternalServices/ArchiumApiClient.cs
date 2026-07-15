using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
    private const string ArchiveEndpoint = "/api/v1/external-portal/archive";
    private const string ReceivedDocumentsOpsEndpoint = "/api/v1/received-documents/ops";

    public ArchiumApiClient(HttpClient httpClient, ILogger<ArchiumApiClient> logger)
        : base(httpClient, logger)
    {
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
        using var content = new MultipartFormDataContent
        {
            { new StringContent(TempBucketName), "bucketName" }
        };

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
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

    public async Task<ArchiveFileResult?> ArchiveFileAsync(
        long folderId, List<UploadedFilePayload> files, bool protocolRequired,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(folderId.ToString()), "archiumFolderId" },
            { new StringContent(protocolRequired.ToString()), "protocol.required" }
        };

        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(file.Content);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, "files", file.FileName);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, ArchiveEndpoint)
        {
            Content = content
        };

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ARCHIUM returned {StatusCode} archiving {FileCount} file(s) into folder {FolderId}",
                (int)response.StatusCode, files.Count, folderId);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<ArchiveFileResult>(json, _jsonOptions);
    }
}