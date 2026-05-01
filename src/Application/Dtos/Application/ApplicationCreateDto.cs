namespace CitizenPortal.Application.Dtos;

public class ApplicationCreateDto
{
    public required Guid UserId { get; set; }
    public required string Subject { get; set; }
    public required string Email { get; set; }
    public required string Body { get; set; }
}
