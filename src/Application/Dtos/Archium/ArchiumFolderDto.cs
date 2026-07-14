namespace CitizenPortal.Application.Dtos;

/// A single folder node as returned by ARCHIUM's External-Portal API (spec 3.4.1.2).
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
    public long? ParentFolderId { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public string FolderCategory { get; set; } = string.Empty;
}

public class CreateFolderResult
{
    public long ArchiumFolderId { get; set; }
    public DateTime Timestamp { get; set; }
}
