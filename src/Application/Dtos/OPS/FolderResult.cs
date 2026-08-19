using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

/// A single folder node as returned by ARCHIUM BackEnd API
public class FoldeResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("parentFolderId")]
    public long? ParentFolderId { get; set; }
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("categoryName")]
    public string CategoryName { get; set; } = string.Empty;
    //public string? Color { get; set; }
    //public string CreatorFullName { get; set; } = string.Empty;
    //public string RoleName { get; set; } = string.Empty;
    //public string OrganizationName { get; set; } = string.Empty;
    //public List<object> FolderUserPermissions { get; set; } = [];
}

public class FolderListResult
{
    public List<FoldeResult> Folders { get; set; } = [];
}
