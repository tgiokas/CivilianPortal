using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Enums;
using CitizenPortal.Domain.Interfaces;

namespace CitizenPortal.Application.Services;

public class ExternalPortalService : IExternalPortalService
{
    private const long MaxFileBytes = 50L * 1024 * 1024; // 50 MB — matches Kestrel MaxRequestBodySize
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls"
    };

    private readonly IArchiumApiClient _archiumClient;
    private readonly IUploadJobRepository _uploadJobRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly IApplicationDbContext _dbContext;
    private readonly IAntivirusScanner _antivirusScanner;
    private readonly ArchiumClientSettings _archiumSettings;
    private readonly KafkaSettings _kafkaSettings;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<ExternalPortalService> _logger;

    public ExternalPortalService(
        IArchiumApiClient archiumClient,
        IUploadJobRepository uploadJobRepo,
        IOutboxRepository outboxRepo,
        IApplicationDbContext dbContext,
        IAntivirusScanner antivirusScanner,
        IOptions<ArchiumClientSettings> archiumOptions,
        IOptions<KafkaSettings> kafkaOptions,
        IErrorCatalog errors,
        ILogger<ExternalPortalService> logger)
    {
        _archiumClient = archiumClient;
        _uploadJobRepo = uploadJobRepo;
        _outboxRepo = outboxRepo;
        _dbContext = dbContext;
        _antivirusScanner = antivirusScanner;
        _archiumSettings = archiumOptions.Value;
        _kafkaSettings = kafkaOptions.Value;
        _errors = errors;
        _logger = logger;
    }

    public async Task<Result<FolderListResult>> ListFoldersAsync(long? parentId, CancellationToken cancellationToken = default)
    {
        var result = await _archiumClient.GetFoldersAsync(parentId, cancellationToken);
        if (result is null)
            return _errors.Fail<FolderListResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        return Result<FolderListResult>.Ok(result);
    }

    public async Task<Result<CreateFolderResult>> CreateFolderAsync(
        CreateFolderRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _archiumClient.CreateFolderAsync(
            request.ParentFolderId, request.FolderName, request.FolderCategory, cancellationToken);

        if (result is null)
            return _errors.Fail<CreateFolderResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        return Result<CreateFolderResult>.Ok(result);
    }

    public async Task<Result<UploadJobAcceptedResult>> SubmitUploadAsync(
        UploadFileRequest request, CancellationToken cancellationToken = default)
    {
        var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "document.pdf" : request.FileName;

        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(request.File);
        }
        catch (FormatException)
        {
            return _errors.Fail<UploadJobAcceptedResult>(ErrorCodes.PORTAL.InvalidApplicationData);
        }

        var (isValid, errorCode) = await ValidateFileAsync(fileName, fileBytes, cancellationToken);
        if (!isValid)
            return _errors.Fail<UploadJobAcceptedResult>(errorCode!);

        var files = new List<UploadedFilePayload>
        {
            new() { FileName = fileName, ContentType = "application/octet-stream", Content = fileBytes }
        };

        foreach (var attachment in request.Attachments)
        {
            byte[] attachmentBytes;
            try
            {
                attachmentBytes = Convert.FromBase64String(attachment.File);
            }
            catch (FormatException)
            {
                return _errors.Fail<UploadJobAcceptedResult>(ErrorCodes.PORTAL.InvalidApplicationData);
            }

            var (attachmentValid, attachmentError) = await ValidateFileAsync(attachment.FileName, attachmentBytes, cancellationToken);
            if (!attachmentValid)
                return _errors.Fail<UploadJobAcceptedResult>(attachmentError!);

            files.Add(new UploadedFilePayload
            {
                FileName = attachment.FileName,
                ContentType = "application/octet-stream",
                Content = attachmentBytes
            });
        }

        return await CreateJobAndPublishAsync(
            callerSystemId: request.CallerSystemId,
            folderId: null,
            protocolRequired: true,
            subject: request.Metadata.Subject,
            files: files,
            cancellationToken: cancellationToken);
    }

    public async Task<Result<UploadJobAcceptedResult>> SubmitArchiveAsync(
        ArchiveFileRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Files.Count == 0)
            return _errors.Fail<UploadJobAcceptedResult>(ErrorCodes.PORTAL.NoFilesProvided);

        foreach (var file in request.Files)
        {
            var (isValid, errorCode) = await ValidateFileAsync(file.FileName, file.Content, cancellationToken);
            if (!isValid)
                return _errors.Fail<UploadJobAcceptedResult>(errorCode!);
        }

        return await CreateJobAndPublishAsync(
            callerSystemId: _archiumSettings.CallerSystemId,
            folderId: request.FolderId,
            protocolRequired: request.ProtocolRequired,
            subject: request.ProtocolSubject,
            files: request.Files,
            cancellationToken: cancellationToken);
    }

    public async Task<Result<UploadJobStatusResult>> GetJobStatusAsync(Guid jobId)
    {
        var job = await _uploadJobRepo.GetByJobIdAsync(jobId);
        if (job is null)
            return _errors.Fail<UploadJobStatusResult>(ErrorCodes.PORTAL.UploadJobNotFound);

        return Result<UploadJobStatusResult>.Ok(MapToStatusResult(job));
    }

    public async Task<Result<RetrievedFileResult>> RetrieveFileAsync(long fileId, CancellationToken cancellationToken = default)
    {
        var result = await _archiumClient.GetFileAsync(fileId, _archiumSettings.CallerSystemId, cancellationToken);
        if (result is null)
            return _errors.Fail<RetrievedFileResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        return Result<RetrievedFileResult>.Ok(result);
    }

    private async Task<Result<UploadJobAcceptedResult>> CreateJobAndPublishAsync(
        string callerSystemId, long? folderId, bool protocolRequired, string? subject,
        List<UploadedFilePayload> files, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid();
        var submittedAt = DateTime.UtcNow;

        using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var job = new UploadJob
            {
                JobId = jobId,
                CallerSystemId = callerSystemId,
                Status = UploadJobStatus.Pending,
                FolderId = folderId,
                ProtocolRequired = protocolRequired,
                Subject = subject,
                SubmittedAt = submittedAt
            };

            await _uploadJobRepo.AddWithoutSaveAsync(job);

            var outboxEvent = new UploadRequestedEvent
            {
                JobId = jobId,
                CallerSystemId = callerSystemId,
                FolderId = folderId,
                ProtocolRequired = protocolRequired,
                Subject = subject,
                Files = files.Select(f => new UploadRequestedFile
                {
                    FileName = f.FileName,
                    ContentType = f.ContentType,
                    Content = Convert.ToBase64String(f.Content)
                }).ToList(),
                SubmittedAt = submittedAt
            };

            var outboxMessage = new OutboxMessage
            {
                EventType = _kafkaSettings.UploadRequestTopic,
                Key = jobId.ToString(),
                Payload = System.Text.Json.JsonSerializer.Serialize(outboxEvent, OutboxJsonOptions)
            };

            await _outboxRepo.AddAsync(outboxMessage);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Upload job {JobId} accepted for caller {CallerSystemId}: {FileCount} file(s), folder={FolderId}, protocolRequired={ProtocolRequired}.",
                jobId, callerSystemId, files.Count, folderId, protocolRequired);

            return Result<UploadJobAcceptedResult>.Ok(new UploadJobAcceptedResult
            {
                JobId = jobId,
                Status = "PENDING",
                SubmittedAt = submittedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create upload job for caller {CallerSystemId}", callerSystemId);
            return _errors.Fail<UploadJobAcceptedResult>(ErrorCodes.PORTAL.OutboxPublishFailed);
        }
    }

    private async Task<(bool IsValid, string? ErrorCode)> ValidateFileAsync(
        string fileName, byte[] content, CancellationToken cancellationToken)
    {
        if (content.Length > MaxFileBytes)
            return (false, ErrorCodes.PORTAL.ArchiveFileTooLarge);

        if (!AllowedExtensions.Contains(Path.GetExtension(fileName)))
            return (false, ErrorCodes.PORTAL.UnsupportedArchiveFileType);

        var isClean = await _antivirusScanner.IsCleanAsync(content, fileName, cancellationToken);
        if (!isClean)
            return (false, ErrorCodes.PORTAL.InvalidFileType);

        return (true, null);
    }

    private static UploadJobStatusResult MapToStatusResult(UploadJob job) => new()
    {
        JobId = job.JobId,
        Status = job.Status.ToString(),
        ArchiumFileId = job.ArchiumFileId,
        ProtocolNumber = job.ProtocolNumber,
        ProtocolYear = job.ProtocolYear,
        ErrorCode = job.ErrorCode,
        ErrorMessage = job.ErrorMessage,
        SubmittedAt = job.SubmittedAt,
        CompletedAt = job.CompletedAt
    };

    private static readonly System.Text.Json.JsonSerializerOptions OutboxJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };
}
