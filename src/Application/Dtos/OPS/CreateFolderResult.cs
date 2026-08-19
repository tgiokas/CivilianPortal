using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

public class CreateFolderResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}