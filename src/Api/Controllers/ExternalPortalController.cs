using Microsoft.AspNetCore.Mvc;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Api.Controllers;

[ApiController]
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
            ProtocolRequired = form.ProtocolRequired
        };

        var result = await _externalPortalService.SubmitArchiveAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [HttpPost("files/upload")]
    public async Task<IActionResult> UploadFile([FromForm] SubmitDocumentRequest request, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.SubmitUploadAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result.Data);
    }

    [HttpGet("files/{fileId:long}")]
    public async Task<IActionResult> GetFile(long fileId, [FromQuery] string? callerSystemId, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.RetrieveFileAsync(fileId, cancellationToken);

        if (!result.Success)
            return NotFound(result);

        return Ok(result.Data);
    }
}