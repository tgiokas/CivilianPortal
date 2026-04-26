using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

/// <summary>
/// Generates the application form PDF that CitizenPortal produces for each
/// submission. The resulting bytes are uploaded to DMS.Storage alongside the
/// citizen-provided attachments. The Application layer depends only on this
/// interface; the concrete PdfSharpCore implementation lives in Infrastructure.
/// </summary>
public interface IApplicationPdfGenerator
{
    /// <summary>
    /// Render the application PDF. Must be deterministic for the same input
    /// (same bytes for same data) to help with debugging/support.
    /// </summary>
    byte[] Generate(ApplicationPdfData data);
}
