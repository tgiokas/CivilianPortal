using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Interfaces;
using CitizenPortal.Infrastructure.ApiClients;
using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Infrastructure.ExternalServices;

public class StorageApiClient : ApiClientBase, IStorageApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string storageUploadEndpoint
        => $"/api/storage/upload";

    public StorageApiClient(HttpClient httpClient, ILogger<StorageApiClient> logger)
        : base(httpClient, logger)
    {
    }

    public async Task<StorageUploadResult?> UploadFileAsync(
        Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, storageUploadEndpoint)
        {
            Content = content
        };
        
        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<StorageApiResponse>(json, JsonOptions);

        if (result is null || string.IsNullOrEmpty(result.FileId))
        {
            _logger.LogError("DMS.Storage returned null or empty fileId for file {FileName}", fileName);
            return null;
        }

        return new StorageUploadResult(result.FileId, fileName, result.FileSize);
    }

}