using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Api.Controllers;

/// Implements the ARCHIUM External-Portal API (interoperability spec, section 3) for
/// the external ΟΠΣ caller: folder browsing/creation and file retrieval proxy to the
/// real ARCHIUM instance; upload/archive are accepted and completed asynchronously
/// (see IExternalPortalService / UploadJob).
[ApiController]
[Authorize]
[Route("api/v1")]
public class ExternalPortalController : ControllerBase
{
    private readonly IExternalPortalService _externalPortalService;

    public ExternalPortalController(IExternalPortalService externalPortalService)
    {
        _externalPortalService = externalPortalService;
    }

    [HttpGet("external-portal/folders")]
    public async Task<IActionResult> GetFolders([FromQuery] long? parentId, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.ListFoldersAsync(parentId, cancellationToken);

        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway, result);

        return Ok(result.Data);
    }

    [HttpPost("external-portal/folders")]
    public async Task<IActionResult> CreateFolder([FromForm] CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.CreateFolderAsync(request, cancellationToken);

        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway, result);

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [HttpPost("external-portal/archive")]
    public async Task<IActionResult> ArchiveFiles([FromForm] ArchiveFileFormRequest form, CancellationToken cancellationToken)
    {
        var files = new List<UploadedFilePayload>();
        foreach (var file in form.Files)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            files.Add(new UploadedFilePayload
            {
                FileName = file.FileName,
                ContentType = file.ContentType,
                Content = stream.ToArray()
            });
        }

        var request = new ArchiveFileRequest
        {
            FolderId = form.ArchiumFolderId,
            Files = files,
            ProtocolSubject = form.Protocol?.Subject,
            ProtocolRequired = form.Protocol?.Required ?? false
        };

        var result = await _externalPortalService.SubmitArchiveAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return Accepted(result.Data);
    }

    [HttpPost("files/upload")]
    public async Task<IActionResult> UploadFile([FromBody] UploadFileRequest request, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.SubmitUploadAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return Accepted(result.Data);
    }

    [HttpGet("files/{fileId:long}")]
    public async Task<IActionResult> GetFile(long fileId, [FromQuery] string? callerSystemId, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.RetrieveFileAsync(fileId, cancellationToken);

        if (!result.Success)
            return NotFound(result);

        return Ok(result.Data);
    }

    [HttpGet("external-portal/jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJobStatus(Guid jobId)
    {
        var result = await _externalPortalService.GetJobStatusAsync(jobId);

        if (!result.Success)
            return NotFound(result);

        return Ok(result.Data);
    }
}
