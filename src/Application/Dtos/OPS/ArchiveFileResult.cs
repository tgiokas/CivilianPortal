namespace CitizenPortal.Application.Dtos;

public class ArchiveFileResult
{
    public long ArchiumFolderId { get; set; }
    public ArchivedFileDto ArchivedFile { get; set; } = new();   
    public DateTime Timestamp { get; set; }
}

public class ArchivedFileDto
{
    public string FileName { get; set; } = string.Empty;
    public long ArchiumFileId { get; set; }
    public List<ArchivedAttachmentDto> Attachments { get; set; } = [];
}

public class ArchivedAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public long ArchiumFileId { get; set; }
}