using Microsoft.AspNetCore.Http;

namespace CitizenPortal.Application.Dtos;

public class ArchiveFileFormRequest
{
    public long ArchiumFolderId { get; set; }
    public List<IFormFile> Files { get; set; } = [];
    public bool ProtocolRequired { get; set; }
}
