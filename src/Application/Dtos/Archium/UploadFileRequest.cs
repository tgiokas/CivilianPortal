namespace CitizenPortal.Application.Dtos;

/// Request body for POST /api/v1/files/upload 
public class UploadFileRequest
{
    public string CallerSystemId { get; set; } = string.Empty;
    public bool DigitalSignatureValidation { get; set; }
    public string? FileName { get; set; }
    public string File { get; set; } = string.Empty; // base64
    public List<UploadFileAttachment> Attachments { get; set; } = [];
}


public class UploadFileAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty; // base64
}
