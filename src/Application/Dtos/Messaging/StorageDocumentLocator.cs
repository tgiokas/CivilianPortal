using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Application.Dtos;

/// <summary>
/// Pointer to a document stored in DMS.Storage, enriched with the metadata
/// the DMS backend needs to route it correctly.
///
/// Breaking change (2026-04): added <see cref="Kind"/>, <see cref="FileName"/>
/// and <see cref="ContentType"/>. The DMS consumer must read <see cref="Kind"/>
/// to distinguish the CitizenPortal-generated application form PDF
/// (<see cref="ApplicationDocumentKind.ApplicationForm"/>) from citizen-uploaded
/// supporting documents (<see cref="ApplicationDocumentKind.Attachment"/>).
/// </summary>
public class StorageDocumentLocator
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;

    ///// <summary>File name as seen by the citizen (for display / download).</summary>
    //public string FileName { get; set; } = string.Empty;

    ///// <summary>MIME type, e.g. "application/pdf".</summary>
    //public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Whether this document is the portal-generated application form or a
    /// citizen-uploaded attachment. Serialized as a string for forward-compat.
    /// </summary>
    public ApplicationDocumentKind Kind { get; set; } = ApplicationDocumentKind.Attachment;
}
