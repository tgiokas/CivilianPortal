using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

public class ProtocolDocumentResult
{
    [JsonPropertyName("documentId")]
    public long? DocumentId { get; set; }
    [JsonPropertyName("fileId")]
    public long FileId { get; set; }
    [JsonPropertyName("protocolNumber")]
    public int? ProtocolNumber { get; set; }
    [JsonPropertyName("protocolYear")]
    public int? ProtocolYear { get; set; }
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }
}