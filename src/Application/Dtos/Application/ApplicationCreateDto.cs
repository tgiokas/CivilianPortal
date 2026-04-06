namespace CitizenPortal.Application.Dtos.App;

public class ApplicationCreateDto
{
    public required string Subject { get; set; }
    public required string Email { get; set; }
    public required string Body { get; set; }
    // Files come as IFormFile in the controller, not here
}
