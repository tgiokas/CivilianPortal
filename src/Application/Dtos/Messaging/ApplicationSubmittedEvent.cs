namespace CitizenPortal.Application.Dtos;

public class ApplicationSubmittedEvent
{
    public Guid ApplicationPublicId { get; set; }
    public string ExternalSystemId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;    
    public string Email { get; set; } = string.Empty;
    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
    public List<StorageDocumentLocator> Documents { get; set; } = [];
    public DateTime SubmittedAt { get; set; }
}
