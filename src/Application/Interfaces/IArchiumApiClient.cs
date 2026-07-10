using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

/// Outbound calls to the real, separately-hosted ARCHIUM External-Portal API
/// (spec section 3) for folder browsing/creation and file retrieval.
/// Upload/archive (3.3/3.5) go through Kafka instead — see IExternalPortalService.
public interface IArchiumApiClient
{
    Task<FolderListResult?> GetFoldersAsync(long? parentId, CancellationToken cancellationToken = default);

    Task<CreateFolderResult?> CreateFolderAsync(
        long? parentFolderId, string folderName, string folderCategory,
        CancellationToken cancellationToken = default);

    Task<RetrievedFileResult?> GetFileAsync(long fileId, string callerSystemId, CancellationToken cancellationToken = default);
}
