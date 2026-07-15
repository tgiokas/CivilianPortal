using System.Text.Json.Serialization;

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