namespace CitizenPortal.Application.Dtos;

public class ApplicationDocumentDto
{
    public string StorageFileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}
