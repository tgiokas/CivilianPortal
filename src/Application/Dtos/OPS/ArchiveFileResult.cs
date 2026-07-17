namespace CitizenPortal.Application.Dtos;

public class ArchiveFileResult
{
    public long ArchiumFolderId { get; set; }
    public List<ArchivedAttachmentDto> ArchivedFile { get; set; } = [];
    public DateTime Timestamp { get; set; }
}

public class ArchivedAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public long ArchiumFileId { get; set; }
}