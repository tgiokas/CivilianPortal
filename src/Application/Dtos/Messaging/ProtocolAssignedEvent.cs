namespace CitizenPortal.Application.Dtos.Messaging;

public class ProtocolAssignedEvent
{
    public Guid ApplicationPublicId { get; set; }
    public string ProtocolNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
