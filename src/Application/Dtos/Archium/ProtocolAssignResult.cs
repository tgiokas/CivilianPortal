using System.Text.Json.Serialization;

namespace CitizenPortal.Application.Dtos;

public class ProtocolAssignResult
{
    public long FileId { get; set; }
    public string? ProtocolNumber { get; set; }
    public string? ProtocolYear { get; set; }
}
   