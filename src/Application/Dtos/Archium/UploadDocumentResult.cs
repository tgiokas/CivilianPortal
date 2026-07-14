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

/// Raw result of a single-file upload to ARCHIUM (no protocol assignment yet).
public class UploadedFileRef
{
    public long FileId { get; set; }
    public DateTime Timestamp { get; set; }
}

/// PLACEHOLDER — shape of ARCHIUM's protocol-lookup response is unconfirmed.
public class ProtocolAssignmentResult
{
    public long FileId { get; set; }
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
    public DateTime Timestamp { get; set; }
}
