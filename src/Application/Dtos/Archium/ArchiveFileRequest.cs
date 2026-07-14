using Microsoft.AspNetCore.Http;

namespace CitizenPortal.Application.Dtos;

public class ArchiveFileFormRequest
{
    public long ArchiumFolderId { get; set; }
    public List<IFormFile> Files { get; set; } = [];
    public bool ProtocolRequired { get; set; }
}

public class ArchiveFileRequest
{
    public long FolderId { get; set; }
    public List<UploadedFilePayload> Files { get; set; } = [];
    public bool ProtocolRequired { get; set; }
}

public class UploadedFilePayload
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
}
