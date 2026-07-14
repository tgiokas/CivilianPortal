using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

/// Outbound calls to the real, separately-hosted ARCHIUM External-Portal API (spec section 3):
/// folder browsing/creation, file retrieval, and synchronous upload/archive with protocol assignment.
public interface IArchiumApiClient
{
    Task<FolderListResult?> GetFoldersAsync(long? parentId, CancellationToken cancellationToken = default);

    Task<CreateFolderResult?> CreateFolderAsync(
        long? parentFolderId, string folderName, string folderCategory,
        CancellationToken cancellationToken = default);

    Task<RetrievedFileResult?> GetFileAsync(long fileId, string callerSystemId, CancellationToken cancellationToken = default);

    /// Uploads a single file to ARCHIUM and returns just its fileId — used for both the main
    /// document and each attachment in the 3.3 upload flow, which now requires a separate
    /// protocol lookup call (see GetProtocolForFileAsync) rather than a combined response.
    Task<UploadedFileRef?> UploadSingleFileAsync(
        string callerSystemId, bool digitalSignatureValidation, string fileName, byte[] file,
        CancellationToken cancellationToken = default);

    /// PLACEHOLDER — ARCHIUM's real contract for looking up the protocol number assigned to an
    /// already-uploaded fileId is not finalized yet. Guessing GET /api/v1/files/{fileId}/protocol
    /// until the real endpoint is confirmed.
    Task<ProtocolAssignmentResult?> GetProtocolForFileAsync(
        long fileId, string callerSystemId, CancellationToken cancellationToken = default);

    Task<ArchiveFileResult?> ArchiveFileAsync(
        long folderId, List<UploadedFilePayload> files, bool protocolRequired,
        CancellationToken cancellationToken = default);
}
