namespace CitizenPortal.Application.Dtos;

public class UploadDocumentResult
{
    public long FileId { get; set; }
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
    public DateTime Timestamp { get; set; }
    public List<UploadedAttachmentDto> Attachments { get; set; } = [];
}

public class UploadedAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public long FileId { get; set; }
}

public class UploadedFileRef
{
    public long FileId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ProtocolAssignmentResult
{
    public long FileId { get; set; }
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
    public DateTime Timestamp { get; set; }
}
