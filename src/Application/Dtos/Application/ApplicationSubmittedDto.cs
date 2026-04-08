namespace CitizenPortal.Application.Dtos;

public class ApplicationSubmittedDto
{
    public Guid TrackingId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
