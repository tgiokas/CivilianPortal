namespace CitizenPortal.Application.Dtos;

public class ProtocolAssignedEvent
{
    public Guid ApplicationPublicId { get; set; }
    public string ProtocolNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<StorageDocumentLocator> Documents { get; set; } = [];
}
