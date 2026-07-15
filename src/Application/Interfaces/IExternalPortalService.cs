using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

/// Implements the ARCHIUM BackEnd API
/// Folder browsing/creation, upload/archive, and retrieval
public interface IExternalPortalService
{
    Task<Result<FolderListResult>> ListFoldersAsync(long? parentId, CancellationToken cancellationToken = default);

    Task<Result<CreateFolderResult>> CreateFolderAsync(
        CreateFolderRequest request, CancellationToken cancellationToken = default);

    Task<Result<SubmitDocumentResult>> SubmitUploadAsync(
        SubmitDocumentRequest request, CancellationToken cancellationToken = default);

    Task<Result<ArchiveFileResult>> SubmitArchiveAsync(
        ArchiveFileRequest request, CancellationToken cancellationToken = default);

    Task<Result<RetrievedFileResult>> RetrieveFileAsync(long fileId, CancellationToken cancellationToken = default);
}