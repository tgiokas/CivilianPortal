using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Infrastructure.ApiClients;

public class StorageApiClient : IStorageApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StorageApiClient> _logger;

    public StorageApiClient(HttpClient httpClient, ILogger<StorageApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<StorageUploadResult?> UploadFileAsync(
        Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync("/api/storage/upload", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("DMS.Storage upload failed ({StatusCode}): {Error}", response.StatusCode, error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<StorageApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (result is null || string.IsNullOrEmpty(result.FileId))
            {
                _logger.LogError("DMS.Storage returned null or empty fileId");
                return null;
            }

            return new StorageUploadResult(result.FileId, fileName, result.FileSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling DMS.Storage for file {FileName}", fileName);
            return null;
        }
    }

    private class StorageApiResponse
    {
        public string FileId { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }
}
