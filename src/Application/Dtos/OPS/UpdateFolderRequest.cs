namespace CitizenPortal.Application.Dtos;

public class UpdateFolderRequest
{
    public string Subject { get; set; } = string.Empty;
    public long ParentId { get; set; }
    public long MetadataId { get; set; }
    public List<FolderDocumentEntry> Documents { get; set; } = [];
}

public class FolderDocumentEntry
{
    public long? DocumentId { get; set; } = null;
    public long FileId { get; set; }
}
