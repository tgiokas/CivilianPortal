namespace CitizenPortal.Application.Dtos;

public class ApplicationDocumentDto
{
    public string StorageBucket { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Kind { get; set; } = string.Empty;
}
