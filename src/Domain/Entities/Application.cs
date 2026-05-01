using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Domain.Entities;

public class Application
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();  // External tracking ID
    public int CitizenUserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;      // Rich text content
    public string Email { get; set; } = string.Empty;      // Contact email for this application
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public string? ProtocolNumber { get; set; }             // Assigned by DMS (e.g. "1001/2026")
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

    // Navigation
    public CitizenUser CitizenUser { get; set; } = null!;
    public List<ApplicationDocument> Documents { get; set; } = [];
}