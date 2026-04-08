using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

public interface IStorageApiClient
{
    Task<StorageUploadResult?> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
}