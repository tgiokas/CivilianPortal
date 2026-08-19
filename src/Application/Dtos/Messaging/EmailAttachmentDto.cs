namespace CitizenPortal.Application.Dtos;

public class EmailAttachmentDto
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
}
