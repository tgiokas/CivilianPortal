using Microsoft.AspNetCore.Http;

using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

/// Outbound calls to the real, separately-hosted ARCHIUM BackEnd API(spec section 3):
/// folder browsing/creation, file retrieval, and synchronous upload/archive with protocol assignment.
public interface IArchiumApiClient
{
    Task<FolderListResult?> GetFoldersAsync(long? parentId, CancellationToken cancellationToken = default);

    Task<CreateFolderResult?> CreateFolderAsync(
        long? parentFolderId, string folderName, string folderCategory,
        CancellationToken cancellationToken = default);

    /// Downloads a file's raw bytes from ARCHIUM.
    Task<DownloadedFileResult?> GetFileAsync(long fileId, CancellationToken cancellationToken = default);

    /// Uploads a single file to ARCHIUM and returns its fileId, used for both the main
    /// document and each attachment
    Task<UploadDocumentResult?> UploadDocumentAsync(
        IFormFile file, string fileName, CancellationToken cancellationToken = default);

    /// Gets the protocol number assigned to an already-uploaded document and its accompanying files.
    Task<ProtocolDocumentResult?> GetProtocolForDocumentAsync(
        long fileId, string subject, string externalIntegration, IReadOnlyCollection<long> accompanyingFileIds,
        CancellationToken cancellationToken = default);

    /// Attaches already-uploaded documents (by fileId) to a folder.
    Task<bool> ArchiveDocumentsToFolderAsync(
        long folderId, UpdateFolderRequest request, CancellationToken cancellationToken = default);
}