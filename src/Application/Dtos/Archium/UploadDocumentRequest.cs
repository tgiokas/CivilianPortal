using Microsoft.AspNetCore.Http;

namespace CitizenPortal.Application.Dtos;

/// Multipart form body for POST /api/v1/files/upload.
public class UploadDocumentRequest
{
    public string CallerSystemId { get; set; } = string.Empty;
    public bool DigitalSignatureValidation { get; set; }
    public string? FileName { get; set; }
    public string? DocumentSubject { get; set; }
    public IFormFile? File { get; set; }
    public List<IFormFile> Attachments { get; set; } = [];
}