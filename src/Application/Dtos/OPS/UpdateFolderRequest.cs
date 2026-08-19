namespace CitizenPortal.Application.Dtos;

public class UpdateFolderRequest
{
    public List<FolderDocumentEntry> Documents { get; set; } = [];
}

public class FolderDocumentEntry
{
    public long? DocumentId { get; set; } = null;
    public long? FileId { get; set; }
}
