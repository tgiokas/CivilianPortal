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

    Task<RetrievedFileResult?> GetFileAsync(long fileId, string callerSystemId, CancellationToken cancellationToken = default);

    /// Uploads a single file to ARCHIUM and returns its fileId, used for both the main
    /// document and each attachment 
    Task<UploadedFileRef?> UploadDocumentAsync(
        IFormFile file, string fileName, CancellationToken cancellationToken = default);

    /// Gettingthe protocol number assigned to an already-uploaded fileId
    Task<ProtocolAssignmentResult?> GetProtocolForFileAsync(
        long fileId, string callerSystemId, CancellationToken cancellationToken = default);

    Task<ArchiveFileResult?> ArchiveFileAsync(
        long folderId, List<UploadedFilePayload> files, bool protocolRequired,
        CancellationToken cancellationToken = default);
}