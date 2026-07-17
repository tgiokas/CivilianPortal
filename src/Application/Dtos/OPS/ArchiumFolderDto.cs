namespace CitizenPortal.Application.Dtos;

/// A single folder node as returned by ARCHIUM BackEnd API
public class ArchiumFolderDto
{
    public long ArchiumFolderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public bool HasChildren { get; set; }
}

public class FolderListResult
{
    public List<ArchiumFolderDto> Folders { get; set; } = [];
}

public class CreateFolderRequest
{
    public string Subject { get; set; } = string.Empty;
    public long? ParentId { get; set; }
}

public class CreateFolderResult
{
    public long Id { get; set; }
}
