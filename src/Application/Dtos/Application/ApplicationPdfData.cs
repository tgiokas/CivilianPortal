namespace CitizenPortal.Application.Dtos;

/// <summary>
/// All data needed to render the application form PDF.
/// Constructed inside <c>ApplicationService.SubmitApplicationAsync</c>
/// just before calling <c>IApplicationPdfGenerator.Generate</c>.
/// </summary>
public class ApplicationPdfData
{
    public Guid ApplicationPublicId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CitizenFirstName { get; set; }
    public string? CitizenLastName { get; set; }
    public string? TaxisNetId { get; set; }
    public DateTime SubmittedAt { get; set; }

    public string CitizenFullName =>
        string.Join(' ', new[] { CitizenFirstName, CitizenLastName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
