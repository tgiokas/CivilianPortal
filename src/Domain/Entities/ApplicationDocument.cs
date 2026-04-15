namespace CitizenPortal.Domain.Entities;

public class ApplicationDocument
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string StorageBucket { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Application Application { get; set; } = null!;
}
