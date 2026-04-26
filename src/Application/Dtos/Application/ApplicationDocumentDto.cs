namespace CitizenPortal.Application.Dtos;

public class ApplicationDocumentDto
{
    public string StorageBucket { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }

    /// <summary>
    /// "ApplicationForm" for the portal-generated PDF, "Attachment" for
    /// citizen-uploaded supporting documents. The UI can use this to render
    /// a dedicated "Αίτηση (PDF)" entry vs a "Συνημμένα" list.
    /// </summary>
    public string Kind { get; set; } = string.Empty;
}
