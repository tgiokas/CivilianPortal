using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

public class ProtocolDocumentResult
{
    [JsonPropertyName("fileId")]
    public long FileId { get; set; }
    [JsonPropertyName("protocolNumber")]
    public string? ProtocolNumber { get; set; }
    [JsonPropertyName("protocolYear")]
    public string? ProtocolYear { get; set; }
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}