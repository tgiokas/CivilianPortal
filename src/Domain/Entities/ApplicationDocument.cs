namespace CitizenPortal.Domain.Entities;

public class ApplicationDocument
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string StorageFileId { get; set; } = string.Empty;  // ID from DMS.Storage
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Application Application { get; set; } = null!;
}
