using Microsoft.AspNetCore.Http;

namespace CitizenPortal.Application.Dtos;

/// Controller-level binding for POST /api/v1/external-portal/archive (spec 3.5, multipart/form-data).
/// The nested Protocol object binds the spec's dotted form fields "protocol.subject" /
/// "protocol.required" via ASP.NET Core's case-insensitive complex-object prefix binding.
public class ArchiveFileFormRequest
{
    public long ArchiumFolderId { get; set; }
    public List<IFormFile> Files { get; set; } = [];
    public ProtocolFormFields? Protocol { get; set; }
}

public class ProtocolFormFields
{
    public string? Subject { get; set; }
    public bool Required { get; set; }
}

/// Service-level payload — files already read into memory, ready to serialize onto the Kafka event.
public class ArchiveFileRequest
{
    public long FolderId { get; set; }
    public List<UploadedFilePayload> Files { get; set; } = [];
    public string? ProtocolSubject { get; set; }
    public bool ProtocolRequired { get; set; }
}

public class UploadedFilePayload
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
}
