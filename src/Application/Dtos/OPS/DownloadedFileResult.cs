namespace CitizenPortal.Application.Dtos;

/// Result raw file bytes streamed from ARCHIUM.
public class DownloadedFileResult
{
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = "application/octet-stream";
    public string? FileName { get; set; }
}
