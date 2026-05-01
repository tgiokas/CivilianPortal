namespace CitizenPortal.Domain.Entities;

public class CitizenUser
{
    public int Id { get; set; }
    public Guid KeycloakUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? TaxisNetId { get; set; }  // AFM or GSIS identifier    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

    // Navigation
    public List<Application> Applications { get; set; } = [];
}