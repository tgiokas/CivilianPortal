using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Application.Services;

public class ExternalPortalService : IExternalPortalService
{
    private const long MaxFileBytes = 50L * 1024 * 1024; // 50 MB — matches Kestrel MaxRequestBodySize
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls"
    };

    private readonly IArchiumApiClient _archiumClient;  
    private readonly ArchiumClientSettings _archiumSettings;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<ExternalPortalService> _logger;

    public ExternalPortalService(
        IArchiumApiClient archiumClient,      
        IOptions<ArchiumClientSettings> archiumOptions,
        IErrorCatalog errors,
        ILogger<ExternalPortalService> logger)
    {
        _archiumClient = archiumClient;     
        _archiumSettings = archiumOptions.Value;
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

    public async Task<Result<UploadFileResult>> SubmitUploadAsync(
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
            return _errors.Fail<UploadFileResult>(ErrorCodes.PORTAL.InvalidApplicationData);
        }

        var (isValid, errorCode) = await ValidateFileAsync(fileName, fileBytes, cancellationToken);
        if (!isValid)
            return _errors.Fail<UploadFileResult>(errorCode!);

        var attachments = new List<UploadedFilePayload>();
        foreach (var attachment in request.Attachments)
        {
            byte[] attachmentBytes;
            try
            {
                attachmentBytes = Convert.FromBase64String(attachment.File);
            }
            catch (FormatException)
            {
                return _errors.Fail<UploadFileResult>(ErrorCodes.PORTAL.InvalidApplicationData);
            }

            var (attachmentValid, attachmentError) = await ValidateFileAsync(attachment.FileName, attachmentBytes, cancellationToken);
            if (!attachmentValid)
                return _errors.Fail<UploadFileResult>(attachmentError!);

            attachments.Add(new UploadedFilePayload
            {
                FileName = attachment.FileName,
                ContentType = "application/octet-stream",
                Content = attachmentBytes
            });
        }

        var result = await _archiumClient.UploadFileAsync(
            request.CallerSystemId, request.DigitalSignatureValidation, fileName, fileBytes, attachments, cancellationToken);

        if (result is null)
            return _errors.Fail<UploadFileResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        _logger.LogInformation(
            "File uploaded for caller {CallerSystemId}: fileId={FileId}, protocol={ProtocolNumber}/{ProtocolYear}.",
            request.CallerSystemId, result.FileId, result.ProtocolNumber, result.ProtocolYear);

        return Result<UploadFileResult>.Ok(result);
    }

    public async Task<Result<ArchiveFileResult>> SubmitArchiveAsync(
        ArchiveFileRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Files.Count == 0)
            return _errors.Fail<ArchiveFileResult>(ErrorCodes.PORTAL.NoFilesProvided);

        foreach (var file in request.Files)
        {
            var (isValid, errorCode) = await ValidateFileAsync(file.FileName, file.Content, cancellationToken);
            if (!isValid)
                return _errors.Fail<ArchiveFileResult>(errorCode!);
        }

        var result = await _archiumClient.ArchiveFileAsync(
            request.FolderId, request.Files, request.ProtocolRequired, cancellationToken);

        if (result is null)
            return _errors.Fail<ArchiveFileResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        _logger.LogInformation(
            "Archived {FileCount} file(s) into ARCHIUM folder {FolderId}: fileId={FileId}, protocol={ProtocolNumber}/{ProtocolYear}.",
            request.Files.Count, request.FolderId, result.ArchivedFile.ArchiumFileId, result.ProtocolNumber, result.ProtocolYear);

        return Result<ArchiveFileResult>.Ok(result);
    }

    public async Task<Result<RetrievedFileResult>> RetrieveFileAsync(long fileId, CancellationToken cancellationToken = default)
    {
        var result = await _archiumClient.GetFileAsync(fileId, _archiumSettings.CallerSystemId, cancellationToken);
        if (result is null)
            return _errors.Fail<RetrievedFileResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        return Result<RetrievedFileResult>.Ok(result);
    }

    private async Task<(bool IsValid, string? ErrorCode)> ValidateFileAsync(
        string fileName, byte[] content, CancellationToken cancellationToken)
    {
        if (content.Length > MaxFileBytes)
            return (false, ErrorCodes.PORTAL.ArchiveFileTooLarge);

        if (!AllowedExtensions.Contains(Path.GetExtension(fileName)))
            return (false, ErrorCodes.PORTAL.UnsupportedArchiveFileType);

        //var isClean = await _antivirusScanner.IsCleanAsync(content, fileName, cancellationToken);
        //if (!isClean)
        //    return (false, ErrorCodes.PORTAL.InvalidFileType);

        return (true, null);
    }
}
