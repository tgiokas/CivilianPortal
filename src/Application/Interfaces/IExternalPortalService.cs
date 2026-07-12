using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

/// Implements the ARCHIUM External-Portal API surface (interoperability spec, section 3)
/// on behalf of CivilianPortal. Folder browsing/creation, upload/archive, and retrieval
/// all proxy synchronously through to the real ARCHIUM instance.
public interface IExternalPortalService
{
    Task<Result<FolderListResult>> ListFoldersAsync(long? parentId, CancellationToken cancellationToken = default);

    Task<Result<CreateFolderResult>> CreateFolderAsync(
        CreateFolderRequest request, CancellationToken cancellationToken = default);

    Task<Result<UploadFileResult>> SubmitUploadAsync(
        UploadFileRequest request, CancellationToken cancellationToken = default);

    Task<Result<ArchiveFileResult>> SubmitArchiveAsync(
        ArchiveFileRequest request, CancellationToken cancellationToken = default);

    Task<Result<RetrievedFileResult>> RetrieveFileAsync(long fileId, CancellationToken cancellationToken = default);
}
