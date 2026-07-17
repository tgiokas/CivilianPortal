using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

/// A single folder node as returned by ARCHIUM BackEnd API
public class FolderDto
{
    public long ArchiumFolderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public bool HasChildren { get; set; }
}

public class FolderListResult
{
    public List<FolderDto> Folders { get; set; } = [];
}
