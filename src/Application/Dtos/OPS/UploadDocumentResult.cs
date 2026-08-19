using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

public class UploadDocumentResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("bucketName")]
    public string BucketName { get; set; } = string.Empty;   
}