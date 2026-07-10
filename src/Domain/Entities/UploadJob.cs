using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Domain.Entities;

/// Tracks an async upload/archive request submitted to ARCHIUM's External-Portal API
/// (section 3.3 / 3.5 of the interoperability spec). Created PENDING when the request
/// is accepted and the Kafka event is published; updated to COMPLETED/FAILED by
/// ExternalPortalUploadResultConsumer once ARCHIUM publishes to upload.result.
public class UploadJob
{
    public int Id { get; set; }
    public Guid JobId { get; set; } = Guid.NewGuid();  // External tracking ID returned to the caller
    public string CallerSystemId { get; set; } = string.Empty;
    public UploadJobStatus Status { get; set; } = UploadJobStatus.Pending;
    public long? FolderId { get; set; }                // ARCHIUM folder id — set for archive-into-folder jobs (3.5)
    public bool ProtocolRequired { get; set; }
    public string? Subject { get; set; }
    public long? ArchiumFileId { get; set; }            // Set on success
    public string? ProtocolNumber { get; set; }         // Set on success when ProtocolRequired
    public string? ProtocolYear { get; set; }
    public string? ErrorCode { get; set; }              // Set on failure
    public string? ErrorMessage { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
