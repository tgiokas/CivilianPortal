using Microsoft.AspNetCore.Http;

namespace CitizenPortal.Application.Dtos;

/// Multipart form body for POST /api/v1/files/upload.
public class SubmitDocumentRequest
{
    public string CallerSystemId { get; set; } = "OPS";
    public string? FileName { get; set; }
    public string? DocumentSubject { get; set; }
    public IFormFile? File { get; set; }
    public List<IFormFile> Attachments { get; set; } = [];
}