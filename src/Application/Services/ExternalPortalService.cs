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

    public async Task<Result<SubmitDocumentResult>> SubmitUploadAsync(
        SubmitDocumentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.File is null)
            return _errors.Fail<SubmitDocumentResult>(ErrorCodes.PORTAL.NoFilesProvided);

        var fileName = string.IsNullOrWhiteSpace(request.FileName) ? request.File.FileName : request.FileName;

        var (isValid, errorCode) = await ValidateFileAsync(request.File, fileName, cancellationToken);
        if (!isValid)
            return _errors.Fail<SubmitDocumentResult>(errorCode!);

        var attachments = new List<(string FileName, IFormFile File)>();
        foreach (var attachment in request.Attachments)
        {
            var attachmentFileName = string.IsNullOrWhiteSpace(attachment.FileName) ? "attachment" : attachment.FileName;
            var (attachmentValid, attachmentError) = await ValidateFileAsync(attachment, attachmentFileName, cancellationToken);
            if (!attachmentValid)
                return _errors.Fail<SubmitDocumentResult>(attachmentError!);

            attachments.Add((attachmentFileName, attachment));
        }

        // Step 1 - upload the main document.
        var uploadedDocument = await _archiumClient.UploadDocumentAsync(
            request.File, fileName, cancellationToken);

        if (uploadedDocument is null)
            return _errors.Fail<SubmitDocumentResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        // Step 2 - upload each attachment separately before requesting a protocol.
        var uploadedAttachments = new List<SubmitedAttachmentDto>();
        foreach (var attachment in attachments)
        {
            var uploadedAttachment = await _archiumClient.UploadDocumentAsync(
                attachment.File, attachment.FileName, cancellationToken);

            if (uploadedAttachment is null)
                return _errors.Fail<SubmitDocumentResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

            uploadedAttachments.Add(new SubmitedAttachmentDto
            {
                FileName = attachment.FileName,
                FileId = uploadedAttachment.PdfId
            });
        }

        // Step 3 - look up the protocol number assigned to the document and uploaded attachments.
        var protocol = await _archiumClient.GetProtocolForDocumentAsync(
            uploadedDocument.PdfId,
            request.DocumentSubject ?? fileName,
            request.CallerSystemId,
            uploadedAttachments.Select(attachment => attachment.FileId).ToList(),
            cancellationToken);

        if (protocol is null)
            return _errors.Fail<SubmitDocumentResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        _logger.LogInformation(
            "File uploaded for caller {CallerSystemId}: fileId={FileId}, protocol={ProtocolNumber}/{ProtocolYear}, {AttachmentCount} attachment(s).",
            request.CallerSystemId, uploadedDocument.PdfId, protocol.ProtocolNumber, protocol.ProtocolYear, uploadedAttachments.Count);

        return Result<SubmitDocumentResult>.Ok(new SubmitDocumentResult
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
            var (isValid, errorCode) = await ValidateFileAsync(file, file.FileName, cancellationToken);
            if (!isValid)
                return _errors.Fail<ArchiveFileResult>(errorCode!);
        }

        // Step 1 - upload each file individually and collect its ARCHIUM fileId.
        var uploadedFiles = new List<(string FileName, long FileId)>();
        foreach (var file in request.Files)
        {
            var uploaded = await _archiumClient.UploadDocumentAsync(file, file.FileName, cancellationToken);

            if (uploaded is null)
                return _errors.Fail<ArchiveFileResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

            uploadedFiles.Add((file.FileName, uploaded.PdfId));
        }

        // Step 2 - attach the uploaded files to the target folder.
        var folderRequest = new UpdateFolderDocumentsRequest
        {
            Subject = uploadedFiles[0].FileName,
            ParentId = request.ArchiumFolderId,
            MetadataId = _archiumSettings.MetadataId,
            Documents = uploadedFiles
                .Select(file => new FolderDocumentEntry { DocumentId = null, FileId = file.FileId })
                .ToList()
        };

        var attached = await _archiumClient.ArchiveDocumentsToFolderAsync(
            request.ArchiumFolderId, folderRequest, cancellationToken);

        if (!attached)
            return _errors.Fail<ArchiveFileResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        var result = new ArchiveFileResult
        {
            ArchiumFolderId = request.ArchiumFolderId,
            ArchivedFile = new ArchivedFileDto
            {
                FileName = uploadedFiles[0].FileName,
                ArchiumFileId = uploadedFiles[0].FileId,
                Attachments = uploadedFiles.Skip(1)
                    .Select(file => new ArchivedAttachmentDto { FileName = file.FileName, ArchiumFileId = file.FileId })
                    .ToList()
            },
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Archived {FileCount} file(s) into ARCHIUM folder {FolderId}: fileId={FileId}.",
            request.Files.Count, request.ArchiumFolderId, result.ArchivedFile.ArchiumFileId);

        return Result<ArchiveFileResult>.Ok(result);
    }

    public async Task<Result<DownloadedFileResult>> RetrieveFileAsync(long fileId, CancellationToken cancellationToken = default)
    {
        var result = await _archiumClient.GetFileAsync(fileId, cancellationToken);
        if (result is null)
            return _errors.Fail<DownloadedFileResult>(ErrorCodes.PORTAL.ArchiumServiceUnavailable);

        return Result<DownloadedFileResult>.Ok(result);
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
}