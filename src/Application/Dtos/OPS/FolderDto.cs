namespace CitizenPortal.Application.Dtos;

/// A single folder node as returned by ARCHIUM BackEnd API
public class FolderDto
{
    public long Id { get; set; }
    public long? ParentFolderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatorFullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public List<object> FolderUserPermissions { get; set; } = [];
}

public class FolderListResult
{
    public List<FolderDto> Folders { get; set; } = [];
}
