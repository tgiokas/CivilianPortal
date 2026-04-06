namespace CitizenPortal.Application.Dtos.App;

public class ApplicationDto
{
    public Guid PublicId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProtocolNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ApplicationDocumentDto> Documents { get; set; } = [];
}
