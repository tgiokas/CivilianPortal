namespace CitizenPortal.Application.Dtos;

/// Returned immediately (HTTP 202) by both POST /files/upload and POST /external-portal/archive,
/// since protocol/archiving completion happens asynchronously via Kafka (see UploadJob).
public class UploadJobAcceptedResult
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime SubmittedAt { get; set; }
}

/// Returned by GET /api/v1/external-portal/jobs/{jobId} so the caller can poll for the final result.
public class UploadJobStatusResult
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? ArchiumFileId { get; set; }
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
