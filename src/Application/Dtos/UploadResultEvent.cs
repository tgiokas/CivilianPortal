namespace CitizenPortal.Application.Dtos;

/// Consumed from the upload.result Kafka topic (spec 3.3/3.5, Φάση 1) once ARCHIUM
/// finishes processing a previously published UploadRequestedEvent.
public class UploadResultEvent
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty; // "SUCCESS" or "ERROR"
    public long? ArchiumFileId { get; set; }
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
