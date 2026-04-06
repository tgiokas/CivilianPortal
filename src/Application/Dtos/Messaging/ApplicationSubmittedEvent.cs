namespace CitizenPortal.Application.Dtos.Messaging;

public class ApplicationSubmittedEvent
{
    public Guid ApplicationPublicId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CitizenTaxisNetId { get; set; }
    public List<string> StorageFileIds { get; set; } = [];
    public DateTime SubmittedAt { get; set; }
}
