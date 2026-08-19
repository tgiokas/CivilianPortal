namespace CitizenPortal.Application.Dtos;

public class CreateFolderRequest
{
    public string Subject { get; set; } = string.Empty;
    public long? ParentId { get; set; }
}