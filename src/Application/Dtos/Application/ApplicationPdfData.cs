namespace CitizenPortal.Application.Dtos;

/// All data needed to render the application form PDF.
public class ApplicationPdfData
{
    public Guid ApplicationPublicId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string CitizenEmail { get; set; } = string.Empty;
    public string? CitizenFirstName { get; set; }
    public string? CitizenLastName { get; set; }    
    public DateTime SubmittedAt { get; set; }

    public string CitizenFullName =>
        string.Join(' ', new[] { CitizenFirstName, CitizenLastName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
