using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

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
    private readonly IAntivirusScanner _antivirusScanner;
    private readonly ArchiumClientSettings _archiumSettings;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<ExternalPortalService> _logger;

    public ExternalPortalService(
        IArchiumApiClient archiumClient,
        IAntivirusScanner antivirusScanner,
        IOptions<ArchiumClientSettings> archiumOptions,
        IErrorCatalog errors,
        ILogger<ExternalPortalService> logger)
    {
        _archiumClient = archiumClient;
        _antivirusScanner = antivirusScanner;
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

    public async Task<Result<UploadDocumentResult>> SubmitUploadAsync(
        UploadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.File is null)
            return _errors.Fail<UploadDocumentResult>(ErrorCodes.PORTAL.NoFilesProvided);

        var fileName = string.IsNullOrWhiteSpace(request.FileName) ? request.File.FileName : request.FileName;

        var (isValid, errorCode) = await ValidateFileAsync(request.File, fileName, cancellationToken);
        if (!isValid)
            return _errors.Fail<UploadDocumentResult>(errorCode!);

        var attachments = new List<(string FileName, IFormFile File)>();
        foreach (var attachment in request.Attachments)
        {
            var attachmentFileName = string.IsNullOrWhiteSpace(attachment.FileName) ? "attachment" : attachment.FileName;
            var (attachmentValid, attachmentError) = await ValidateFileAsync(attachment, attachmentFileName, cancellationToken);
            if (!attachmentValid)
                return _errors.Fail<UploadDocumentResult>(attachmentError!);

            attachments.Add((attachmentFileName, attachment));
        }

        // Step 1 - upload the main document.
        var uploadedDocument = await _archiumClient.UploadDocumentAsync(
            request.File, fileName, cancellationToken);

        if (uploadedDocument is null)
            return _errors.Fail<UploadDocumentResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        // Step 2 - look up the protocol number assigned to it.
        var protocol = await _archiumClient.GetProtocolForFileAsync(
            uploadedDocument.PdfId, request.CallerSystemId, cancellationToken);

        if (protocol is null)
            return _errors.Fail<UploadDocumentResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        // Step 3 - upload each attachment separately
        var uploadedAttachments = new List<UploadedAttachmentDto>();
        foreach (var attachment in attachments)
        {
            var uploadedAttachment = await _archiumClient.UploadDocumentAsync(
                attachment.File, attachment.FileName, cancellationToken);

            if (uploadedAttachment is null)
                return _errors.Fail<UploadDocumentResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

            uploadedAttachments.Add(new UploadedAttachmentDto
            {
                FileName = attachment.FileName,
                FileId = uploadedAttachment.PdfId
            });
        }

        _logger.LogInformation(
            "File uploaded for caller {CallerSystemId}: fileId={FileId}, protocol={ProtocolNumber}/{ProtocolYear}, {AttachmentCount} attachment(s).",
            request.CallerSystemId, uploadedDocument.PdfId, protocol.ProtocolNumber, protocol.ProtocolYear, uploadedAttachments.Count);

        return Result<UploadDocumentResult>.Ok(new UploadDocumentResult
        {
            FileId = uploadedDocument.PdfId,
            ProtocolNumber = protocol.ProtocolNumber,
            ProtocolYear = protocol.ProtocolYear,
            Timestamp = protocol.Timestamp,
            Attachments = uploadedAttachments
        });
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
        Microsoft.AspNetCore.Http.IFormFile file, string fileName, CancellationToken cancellationToken)
    {
        if (file.Length > MaxFileBytes)
            return (false, ErrorCodes.PORTAL.ArchiveFileTooLarge);

        if (!AllowedExtensions.Contains(Path.GetExtension(fileName)))
            return (false, ErrorCodes.PORTAL.UnsupportedArchiveFileType);

        //await using var stream = file.OpenReadStream();
        //var isClean = await _antivirusScanner.IsCleanAsync(stream, fileName, cancellationToken);
        //if (!isClean)
        //    return (false, ErrorCodes.PORTAL.InvalidFileType);

        return (true, null);
    }

    private async Task<(bool IsValid, string? ErrorCode)> ValidateFileAsync(
        string fileName, byte[] content, CancellationToken cancellationToken)
    {
        if (content.Length > MaxFileBytes)
            return (false, ErrorCodes.PORTAL.ArchiveFileTooLarge);

        if (!AllowedExtensions.Contains(Path.GetExtension(fileName)))
            return (false, ErrorCodes.PORTAL.UnsupportedArchiveFileType);

        //using var stream = new MemoryStream(content);
        //var isClean = await _antivirusScanner.IsCleanAsync(stream, fileName, cancellationToken);
        //if (!isClean)
        //    return (false, ErrorCodes.PORTAL.InvalidFileType);

        return (true, null);
    }
}