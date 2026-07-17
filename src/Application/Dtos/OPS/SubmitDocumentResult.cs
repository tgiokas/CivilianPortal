namespace CitizenPortal.Application.Dtos;

public class SubmitDocumentResult
{
    public long FileId { get; set; }
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
    public DateTime Timestamp { get; set; }
    public List<SubmitedAttachmentDto> Attachments { get; set; } = [];
}

public class SubmitedAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public long FileId { get; set; }
}