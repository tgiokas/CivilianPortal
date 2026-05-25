namespace CitizenPortal.Application.Dtos;

public class CitizenUserDto
{
    public Guid KeycloakUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FatherName { get; set; }
    public string? VatId { get; set; }
    public bool LegalEntity { get; set; }
}