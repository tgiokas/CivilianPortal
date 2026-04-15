using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Interfaces;
using CitizenPortal.Infrastructure.ApiClients;
using CitizenPortal.Application.Dtos;
using System.Net.Http.Json;

namespace CitizenPortal.Infrastructure.ExternalServices;

public class StorageApiClient : ApiClientBase, IStorageApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string storageUploadEndpoint
        => $"/api/storage/upload";

    private static string storageDeleteEndpoint
        => $"/api/storage/delete";

    public StorageApiClient(HttpClient httpClient, ILogger<StorageApiClient> logger)
        : base(httpClient, logger)
    {
    }

    public async Task<StorageUploadResult?> UploadFileAsync(
        string bucket, string key,
        Stream fileStream, string fileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(bucket), "bucket");
        content.Add(new StringContent(key), "key");

        var request = new HttpRequestMessage(HttpMethod.Post, storageUploadEndpoint)
        {
            Content = content
        };

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<StorageApiResponse>(json, JsonOptions);

        if (result is null || !result.Success || result.Data is null)
        {
            _logger.LogError("DMS.Storage returned unsuccessful response for {Bucket}/{Key}", bucket, key);
            return null;
        }

        return new StorageUploadResult(
            result.Data.Bucket,
            result.Data.Key,
            fileName,
            result.Data.Size);
    }

    public async Task<bool> DeleteFileAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, storageDeleteEndpoint)
            {
                Content = JsonContent.Create(new { bucket, key })
            };

            var response = await SendRequestAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete orphaned file {Bucket}/{Key} from DMS.Storage", bucket, key);
            return false;
        }
    }

}