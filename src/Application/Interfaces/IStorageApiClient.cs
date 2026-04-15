using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

public interface IStorageApiClient
{
    Task<StorageUploadResult?> UploadFileAsync(
         string bucket, string key,
         Stream fileStream, string fileName, string contentType,
         CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string bucket, string key, CancellationToken cancellationToken = default);
}