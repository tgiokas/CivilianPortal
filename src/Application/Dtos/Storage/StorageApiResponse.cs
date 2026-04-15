namespace CitizenPortal.Application.Dtos;

public class StorageApiResponse
{
    public bool Success { get; set; }
    public StorageApiResponseData? Data { get; set; }
}

public class StorageApiResponseData
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public long Size { get; set; }
    public string ContentType { get; set; } = string.Empty;
}
