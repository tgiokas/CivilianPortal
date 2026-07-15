using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

public class ArchiumProtocolResult
{
    public long FileId { get; set; }
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
}
   