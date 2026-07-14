namespace CitizenPortal.Application.Dtos;

/// Synchronous success response for POST /api/v1/external-portal/archive (spec 3.5.1.2.4).
public class ArchiveFileResult
{
    public long ArchiumFolderId { get; set; }
    public ArchivedFileDto ArchivedFile { get; set; } = new();
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
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
