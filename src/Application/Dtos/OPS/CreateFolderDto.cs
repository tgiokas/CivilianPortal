using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

public class CreateFolderRequest
{
    public string Subject { get; set; } = string.Empty;
    public long? ParentId { get; set; }
}

public class CreateFolderResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}