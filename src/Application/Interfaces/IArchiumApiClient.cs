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

    Task<UploadFileResult?> UploadFileAsync(
        string callerSystemId, bool digitalSignatureValidation,
        string fileName, byte[] file, List<UploadedFilePayload> attachments,
        CancellationToken cancellationToken = default);

    Task<ArchiveFileResult?> ArchiveFileAsync(
        long folderId, List<UploadedFilePayload> files, bool protocolRequired,
        CancellationToken cancellationToken = default);
}
