using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

public class ProtocolDocumentRequest
{   
    public long FileId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string ExternalIntegration { get; set; } = string.Empty;
    public List<long> AccompaningFiles { get; set; } = [];
}