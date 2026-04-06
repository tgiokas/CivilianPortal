namespace CitizenPortal.Application.Interfaces;

public record ErrorInfo(string Code, string Message);

public interface IErrorCatalog
{
    ErrorInfo GetError(string code);
}

public interface IMessagePublisher
{
    Task PublishJsonAsync<T>(
        string route,
        string key,
        T payload,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default);
}

public interface IStorageApiClient
{
    Task<StorageUploadResult?> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
}

public record StorageUploadResult(string StorageFileId, string FileName, long FileSize);
