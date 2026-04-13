using System.Text.Json;
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
    private readonly IApplicationRepository _applicationRepo;
    private readonly ICitizenUserRepository _citizenUserRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly IStorageApiClient _storageClient;
    private readonly IApplicationDbContext _dbContext;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<ApplicationService> _logger;

    public ApplicationService(
        IApplicationRepository applicationRepo,
        ICitizenUserRepository citizenUserRepo,
        IOutboxRepository outboxRepo,
        IStorageApiClient storageClient,
        IApplicationDbContext dbContext,
        IErrorCatalog errors,
        ILogger<ApplicationService> logger)
    {
        _applicationRepo = applicationRepo;
        _citizenUserRepo = citizenUserRepo;
        _outboxRepo = outboxRepo;
        _storageClient = storageClient;
        _dbContext = dbContext;
        _errors = errors;
        _logger = logger;
    }

    public async Task<Result<ApplicationSubmittedDto>> SubmitApplicationAsync(       
        ApplicationCreateDto request,
        List<IFormFile>? files,
        CancellationToken cancellationToken = default)
    {
        // 1. Get or verify citizen user
        var citizenUser = await _citizenUserRepo.GetByKeycloakUserIdAsync(request.KeycloakUserId);
        if (citizenUser is null)
        {
            return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.UserNotFound);
        }

        // 2. Upload files to DMS.Storage (before transaction — external call)
        var uploadedDocs = new List<ApplicationDocument>();
        if (files is not null && files.Count > 0)
        {
            foreach (var file in files)
            {
                try
                {
                    using var stream = file.OpenReadStream();
                    var uploadResult = await _storageClient.UploadFileAsync(
                        stream, file.FileName, file.ContentType, cancellationToken);

                    if (uploadResult is null)
                    {
                        _logger.LogError("Failed to upload file {FileName} to DMS.Storage", file.FileName);
                        return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.FileUploadFailed);
                    }

                    uploadedDocs.Add(new ApplicationDocument
                    {
                        StorageFileId = uploadResult.StorageFileId,
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = uploadResult.FileSize
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Storage service error uploading {FileName}", file.FileName);
                    return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.StorageServiceUnavailable);
                }
            }
        }

        // 3. Single DB transaction: save Application + Documents + OutboxMessage
        //    This is the Outbox Pattern — if any of these fail, everything rolls back.
        //    No Kafka message is sent unless the DB commit succeeds.
        using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var application = new Domain.Entities.Application
            {
                CitizenUserId = citizenUser.Id,
                Subject = request.Subject,
                Body = request.Body,
                Email = request.Email,
                Status = ApplicationStatus.Submitted,
                Documents = uploadedDocs
            };

            await _applicationRepo.AddAsync(application);

            // Create outbox message in the same transaction
            var outboxEvent = new ApplicationSubmittedEvent
            {
                ApplicationPublicId = application.PublicId,
                Subject = application.Subject,
                Body = application.Body,
                Email = application.Email,
                CitizenTaxisNetId = citizenUser.TaxisNetId,
                StorageFileIds = uploadedDocs.Select(d => d.StorageFileId).ToList(),
                SubmittedAt = application.CreatedAt
            };

            var outboxMessage = new OutboxMessage
            {
                EventType = "citizen.application.submitted",
                Key = application.PublicId.ToString(),
                Payload = JsonSerializer.Serialize(outboxEvent)
            };

            await _outboxRepo.AddAsync(outboxMessage);

            // Commit: both Application and OutboxMessage are saved atomically
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Application {PublicId} submitted by citizen {KeycloakUserId} with {DocCount} documents. Outbox message created.",
                application.PublicId, request.KeycloakUserId, uploadedDocs.Count);

            return Result<ApplicationSubmittedDto>.Ok(new ApplicationSubmittedDto
            {
                TrackingId = application.PublicId,
                Status = ApplicationStatus.Submitted.ToString(),
                Message = "Application submitted successfully. You will receive a protocol number via email."
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to save application for citizen {KeycloakUserId}", request.KeycloakUserId);
            return _errors.Fail<ApplicationSubmittedDto>(ErrorCodes.PORTAL.ApplicationCreateFailed);
        }
    }

    public async Task<Result<ApplicationDto>> GetApplicationAsync(Guid keycloakUserId, Guid publicId)
    {
        var citizenUser = await _citizenUserRepo.GetByKeycloakUserIdAsync(keycloakUserId);
        if (citizenUser is null)
            return _errors.Fail<ApplicationDto>(ErrorCodes.PORTAL.UserNotFound);

        var application = await _applicationRepo.GetByPublicIdAsync(publicId);
        if (application is null || application.CitizenUserId != citizenUser.Id)
            return _errors.Fail<ApplicationDto>(ErrorCodes.PORTAL.ApplicationNotFound);

        return Result<ApplicationDto>.Ok(MapToDto(application));
    }

    public async Task<Result<PagedResult<ApplicationDto>>> GetApplicationsAsync(
        Guid keycloakUserId, ApplicationQueryParams queryParams)
    {
        var citizenUser = await _citizenUserRepo.GetByKeycloakUserIdAsync(keycloakUserId);
        if (citizenUser is null)
            return _errors.Fail<PagedResult<ApplicationDto>>(ErrorCodes.PORTAL.UserNotFound);

        var applications = await _applicationRepo.GetByCitizenUserIdAsync(citizenUser.Id);

        if (queryParams.Status.HasValue)
            applications = applications.Where(a => a.Status == queryParams.Status.Value).ToList();

        var total = applications.Count;
        var paged = applications
            .OrderByDescending(a => a.CreatedAt)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .Select(MapToDto)
            .ToList();

        return Result<PagedResult<ApplicationDto>>.Ok(new PagedResult<ApplicationDto>
        {
            Items = paged,
            TotalCount = total,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        });
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

        await _applicationRepo.UpdateStatusAsync(application.Id, newStatus, protocolEvent.ProtocolNumber);

        _logger.LogInformation(
            "Application {PublicId} updated: status={Status}, protocol={ProtocolNumber}",
            protocolEvent.ApplicationPublicId, newStatus, protocolEvent.ProtocolNumber);

        return Result<bool>.Ok(true, "Status updated");
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
            StorageFileId = d.StorageFileId,
            FileName = d.FileName,
            ContentType = d.ContentType,
            FileSize = d.FileSize
        }).ToList()
    };
}
