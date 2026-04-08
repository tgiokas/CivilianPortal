using CitizenPortal.Application.Dtos.Storage;

namespace CitizenPortal.Application.Interfaces;

public interface IStorageApiClient
{
    Task<StorageUploadResult?> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
}