namespace CitizenPortal.Application.Dtos;

/// Published to the upload.request Kafka topic (spec 3.3/3.5, Φάση 1) when
/// CivilianPortal accepts an upload/archive request. ARCHIUM's API consumes it,
/// stores the file(s), assigns a protocol number if requested, and publishes the
/// outcome to upload.result.
public class UploadRequestedEvent
{
    public Guid JobId { get; set; }
    public string CallerSystemId { get; set; } = string.Empty;
    public long? FolderId { get; set; }
    public bool ProtocolRequired { get; set; }
    public string? Subject { get; set; }
    public List<UploadRequestedFile> Files { get; set; } = [];
    public DateTime SubmittedAt { get; set; }
}

public class UploadRequestedFile
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // base64
}
