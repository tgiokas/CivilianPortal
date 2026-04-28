using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Application.Dtos;

/// Pointer to a document stored in DMS.Storage
public class StorageDocumentLocator
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public ApplicationDocumentKind Kind { get; set; } = ApplicationDocumentKind.Attachment;
}
