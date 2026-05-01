using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Enums;
using CitizenPortal.Domain.Interfaces;

namespace CitizenPortal.Application.Services;

public class ApplicationService : IApplicationService
{
    private const string CitizenPortalBucket = "citizen-portal";
    private const string ApplicationFormFileName = "application-form.pdf";
    private const string ApplicationFormContentType = "application/pdf";
    private const string ApplicationFormKeyTemplate = "applications/{0}/generated/application-form.pdf";
    private const long MaxAttachmentBytes = 500L * 1024 * 1024; // 500 MB storage backend limit

    private readonly IApplicationRepository _applicationRepo;
    private readonly ICitizenUserRepository _citizenUserRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly IStorageApiClient _storageClient;
    private readonly IApplicationPdfGenerator _pdfGenerator;
    private readonly IApplicationDbContext _dbContext;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<ApplicationService> _logger;

    private static readonly JsonSerializerOptions OutboxJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ApplicationService(
        IApplicationRepository applicationRepo,
        ICitizenUserRepository citizenUserRepo,
        IOutboxRepository outboxRepo,
        IStorageApiClient storageClient,
        IApplicationPdfGenerator pdfGenerator,
        IApplicationDbContext dbContext,
        IErrorCatalog errors,
        ILogger<ApplicationService> logger)
    {
        _applicationRepo = applicationRepo;
        _citizenUserRepo = citizenUserRepo;
        _outboxRepo = outboxRepo;
        _storageClient = storageClient;
        _pdfGenerator = pdfGenerator;
        _dbContext = dbContext;
        _errors = errors;
        _logger = logger;
    }

    public async Task<Result<ApplicationSubmittedDto>> SubmitApplicationAsync(
        ApplicationCreateDto request,
        List<IFormFile>? files,
        string externalSystemId,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify citizen user exists
        var citizenUser = await _citizenUserRepo.GetByKeycloakUserIdReadOnlyAsync(request.UserId);
        if (citizenUser is null)
        {
            return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.UserNotFound);
        }

        // Generate PublicId early so we can use it in storage keys
        var applicationPublicId = Guid.NewGuid();
        var submittedAt = DateTime.UtcNow;

        // 2. Generate the application form PDF (CitizenPortal is now the source
        //    of truth for this document; DMS no longer generates it).
        byte[] pdfBytes;
        try
        {
            pdfBytes = _pdfGenerator.Generate(new ApplicationPdfData
            {
                ApplicationPublicId = applicationPublicId,
                Subject = request.Subject,
                Body = request.Body,
                CitizenEmail = request.Email,
                CitizenFirstName = citizenUser.FirstName,
                CitizenLastName = citizenUser.LastName,
                SubmittedAt = submittedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate application PDF for citizen {CitizenUserId}",
                request.UserId);
            return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.PdfGenerationFailed);
        }

        // 3. Upload everything to DMS.Storage.
        //    Order matters for compensation: the generated form goes first so
        //    if any subsequent attachment fails, CleanupUploadedFilesAsync
        //    will tear down the form too.
        var uploadedDocs = new List<ApplicationDocument>();

        // 3a. Application form PDF
        var formKey = string.Format(ApplicationFormKeyTemplate, applicationPublicId);
        try
        {
            using var pdfStream = new MemoryStream(pdfBytes);
            var formUpload = await _storageClient.UploadFileAsync(
                CitizenPortalBucket, formKey,
                pdfStream, ApplicationFormFileName, ApplicationFormContentType,
                cancellationToken);

            if (formUpload is null)
            {
                _logger.LogError(
                    "Failed to upload generated application PDF to DMS.Storage for {PublicId}",
                    applicationPublicId);
                return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.FileUploadFailed);
            }

            uploadedDocs.Add(new ApplicationDocument
            {
                StorageBucket = formUpload.Bucket,
                StorageKey = formUpload.Key,
                FileName = ApplicationFormFileName,
                ContentType = ApplicationFormContentType,
                FileSize = formUpload.FileSize,
                Kind = ApplicationDocumentKind.ApplicationForm
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Storage service error while uploading generated application PDF for {PublicId}",
                applicationPublicId);
            return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.StorageServiceUnavailable);
        }

        // 3b. Citizen-uploaded attachments
        if (files is not null && files.Count > 0)
        {
            foreach (var file in files)
            {
                if (file.Length > MaxAttachmentBytes)
                {
                    _logger.LogWarning(
                        "Attachment {FileName} rejected: size {Size} bytes exceeds the {Limit} MB limit.",
                        file.FileName, file.Length, MaxAttachmentBytes / 1024 / 1024);
                    await CleanupUploadedFilesAsync(uploadedDocs, cancellationToken);
                    return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.FileTooLarge);
                }

                var storageKey = $"applications/{applicationPublicId}/attachments/{Guid.NewGuid():N}-{file.FileName}";

                try
                {
                    using var stream = file.OpenReadStream();
                    var uploadResult = await _storageClient.UploadFileAsync(
                        CitizenPortalBucket, storageKey,
                        stream, file.FileName, file.ContentType, cancellationToken);

                    if (uploadResult is null)
                    {
                        _logger.LogError("Failed to upload attachment {FileName} to DMS.Storage", file.FileName);
                        await CleanupUploadedFilesAsync(uploadedDocs, cancellationToken);
                        return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.FileUploadFailed);
                    }

                    uploadedDocs.Add(new ApplicationDocument
                    {
                        StorageBucket = uploadResult.Bucket,
                        StorageKey = uploadResult.Key,
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = uploadResult.FileSize,
                        Kind = ApplicationDocumentKind.Attachment
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Storage service error uploading attachment {FileName}", file.FileName);
                    await CleanupUploadedFilesAsync(uploadedDocs, cancellationToken);
                    return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.StorageServiceUnavailable);
                }
            }
        }

        // 4. Single DB transaction: save Application + Documents + OutboxMessage
        using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var application = new Domain.Entities.Application
            {
                PublicId = applicationPublicId,
                UserId = citizenUser.Id,
                Subject = request.Subject,
                Body = request.Body,
                Email = request.Email,
                Status = ApplicationStatus.Submitted,
                CreatedAt = submittedAt,
                Documents = uploadedDocs
            };

            await _applicationRepo.AddWithoutSaveAsync(application);

            var outboxEvent = new ApplicationSubmittedEvent
            {
                ApplicationPublicId = application.PublicId,
                ExternalSystemId = externalSystemId,
                Subject = application.Subject,
                Email = application.Email,
                Documents = uploadedDocs.Select(d => new StorageDocumentLocator
                {
                    Bucket = d.StorageBucket,
                    Key = d.StorageKey,
                    Kind = d.Kind
                }).ToList(),
                SubmittedAt = application.CreatedAt
            };

            var outboxMessage = new OutboxMessage
            {
                EventType = "citizen.application.submitted",
                Key = application.PublicId.ToString(),
                Payload = JsonSerializer.Serialize(outboxEvent, OutboxJsonOptions)
            };

            await _outboxRepo.AddAsync(outboxMessage);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Application {PublicId} submitted by citizen {CitizenUserId}: " +
                "1 application form + {AttachmentCount} attachment(s). Outbox message created.",
                application.PublicId, request.UserId, uploadedDocs.Count - 1);

            return Result<ApplicationSubmittedDto>.Ok(new ApplicationSubmittedDto
            {
                TrackingId = application.PublicId,
                Status = ApplicationStatus.Submitted.ToString(),
                Message = "Application submitted successfully. You will receive a protocol number via email."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DB transaction failed for citizen {CitizenUserId}. Attempting to clean up {DocCount} uploaded files.",
                request.UserId, uploadedDocs.Count);

            await CleanupUploadedFilesAsync(uploadedDocs, cancellationToken);

            return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.ApplicationCreatedFailed);
        }
    }

    public async Task<Result<ApplicationDto>> GetApplicationAsync(Guid publicId)
    {

        var application = await _applicationRepo.GetByPublicIdAsync(publicId);
        if (application is null)
            return _errors.Fail<ApplicationDto>(ErrorCodes.PORTAL.ApplicationNotFound);

        return Result<ApplicationDto>.Ok(MapToDto(application));
    }

    public async Task<Result<List<ApplicationDto>>> GetUserApplicationsAsync(Guid keycloakUserId)
    {
        var citizenUser = await _citizenUserRepo.GetByKeycloakUserIdReadOnlyAsync(keycloakUserId);
        if (citizenUser is null)
            return _errors.Fail<List<ApplicationDto>>(ErrorCodes.PORTAL.UserNotFound);

        var applications = await _applicationRepo.GetByUserIdAsync(citizenUser.Id);

        var result = applications
            .Select(MapToDto)
            .ToList();

        return Result<List<ApplicationDto>>.Ok(result);
    }

    /// Called by the Kafka consumer when DMS publishes a protocol-assigned event.
    public async Task<Result<bool>> UpdateStatusFromDmsAsync(ProtocolAssignedEvent protocolEvent)
    {
        var application = await _applicationRepo.GetByPublicIdAsync(protocolEvent.ApplicationPublicId);
        if (application is null)
        {
            _logger.LogWarning("Received protocol assignment for unknown application {PublicId}",
                protocolEvent.ApplicationPublicId);
            return _errors.Fail<bool>(ErrorCodes.PORTAL.ApplicationNotFound);
        }

        var newStatus = Enum.TryParse<ApplicationStatus>(protocolEvent.Status, true, out var parsed)
            ? parsed
            : ApplicationStatus.Registered;

        var updated = await _applicationRepo.UpdateStatusAsync(
            application.Id, newStatus, protocolEvent.ProtocolNumber);

        if (!updated)
        {
            _logger.LogWarning(
                "Application {PublicId} (Id={Id}) disappeared between read and write while " +
                "applying protocol assignment.",
                protocolEvent.ApplicationPublicId, application.Id);
            return _errors.Fail<bool>(ErrorCodes.PORTAL.ApplicationNotFound);
        }

        _logger.LogInformation(
            "Application {PublicId} updated: status={Status}, protocol={ProtocolNumber}.",
            protocolEvent.ApplicationPublicId, newStatus, protocolEvent.ProtocolNumber);

        return Result<bool>.Ok(true, "Status updated");
    }

    private async Task CleanupUploadedFilesAsync(List<ApplicationDocument> uploadedDocs, CancellationToken cancellationToken)
    {
        foreach (var doc in uploadedDocs)
        {
            var deleted = await _storageClient.DeleteFileAsync(
                doc.StorageBucket, doc.StorageKey, cancellationToken);

            if (!deleted)
            {
                _logger.LogWarning(
                    "Failed to clean up orphaned file {Bucket}/{Key} ({FileName}, kind={Kind}). Manual cleanup may be required.",
                    doc.StorageBucket, doc.StorageKey, doc.FileName, doc.Kind);
            }
        }
    }

    private static ApplicationDto MapToDto(Domain.Entities.Application app) => new()
    {
        PublicId = app.PublicId,
        Subject = app.Subject,
        Email = app.Email,
        Body = app.Body,
        Status = app.Status.ToString(),
        ProtocolNumber = app.ProtocolNumber,
        CreatedAt = app.CreatedAt,
        Documents = app.Documents.Select(d => new ApplicationDocumentDto
        {
            StorageBucket = d.StorageBucket,
            StorageKey = d.StorageKey,
            FileName = d.FileName,
            ContentType = d.ContentType,
            FileSize = d.FileSize,
            Kind = d.Kind.ToString()
        }).ToList()
    };
}