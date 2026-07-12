namespace CitizenPortal.Application.Dtos;

/// Synchronous success response for POST /api/v1/files/upload (spec 3.3.1).
public class UploadFileResult
{
    public long FileId { get; set; }
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
    public DateTime Timestamp { get; set; }
}
