using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

/// Implements the ARCHIUM External-Portal API surface (interoperability spec, section 3)
/// on behalf of CivilianPortal. Folder browsing/creation and retrieval proxy straight
/// through to ARCHIUM; upload/archive are accepted immediately and completed
/// asynchronously via Kafka (see UploadJob).
public interface IExternalPortalService
{
    Task<Result<FolderListResult>> ListFoldersAsync(long? parentId, CancellationToken cancellationToken = default);

    Task<Result<CreateFolderResult>> CreateFolderAsync(
        CreateFolderRequest request, CancellationToken cancellationToken = default);

    Task<Result<UploadJobAcceptedResult>> SubmitUploadAsync(
        UploadFileRequest request, CancellationToken cancellationToken = default);

    Task<Result<UploadJobAcceptedResult>> SubmitArchiveAsync(
        ArchiveFileRequest request, CancellationToken cancellationToken = default);

    Task<Result<UploadJobStatusResult>> GetJobStatusAsync(Guid jobId);

    Task<Result<RetrievedFileResult>> RetrieveFileAsync(long fileId, CancellationToken cancellationToken = default);
}
