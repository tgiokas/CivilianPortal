using Microsoft.AspNetCore.Http;

namespace CitizenPortal.Application.Dtos;

public class ArchiveFileRequest
{
    public long ArchiumFolderId { get; set; }
    public List<IFormFile> Files { get; set; } = [];
    // Keep this naming to singular for model-binding reasons.
    public List<long>? DocumentId { get; set; }
}
