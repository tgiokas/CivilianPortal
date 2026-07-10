using System.Text.Json;
using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Infrastructure.ApiClients;

namespace CitizenPortal.Infrastructure.ExternalServices;

/// Outbound HTTP client for ARCHIUM's External-Portal API (spec section 3).
/// No Authorization header is attached — client-credentials token acquisition
/// is out of scope for this pass.
public class ArchiumApiClient : ApiClientBase, IArchiumApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string FoldersEndpoint = "/api/v1/external-portal/folders";
    private const string FilesEndpoint = "/api/v1/files";

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
        return JsonSerializer.Deserialize<FolderListResult>(json, JsonOptions);
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
        return JsonSerializer.Deserialize<CreateFolderResult>(json, JsonOptions);
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
        return JsonSerializer.Deserialize<RetrievedFileResult>(json, JsonOptions);
    }
}
