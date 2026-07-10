namespace CitizenPortal.Application.Dtos;

/// Result of GET /api/v1/files/{fileId} (spec 3.6.1) — synchronous passthrough to ARCHIUM.
public class RetrievedFileResult
{
    public long FileId { get; set; }
    public string File { get; set; } = string.Empty; // base64
    public RetrievedFileMetadata Metadata { get; set; } = new();
}

public class RetrievedFileMetadata
{
    public string ArchivePath { get; set; } = string.Empty;
    public string? ProtocolNumber { get; set; }
    public DateTime? ProtocolDate { get; set; }
    public DateTime UploadedAt { get; set; }
    public string CallerSystemId { get; set; } = string.Empty;
}
