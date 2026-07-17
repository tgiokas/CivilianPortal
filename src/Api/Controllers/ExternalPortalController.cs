using Microsoft.AspNetCore.Mvc;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ExternalPortalController : ControllerBase
{
    private readonly IExternalPortalService _externalPortalService;

    public ExternalPortalController(IExternalPortalService externalPortalService)
    {
        _externalPortalService = externalPortalService;
    }

    [HttpGet("folders")]
    public async Task<IActionResult> GetFolders([FromQuery] long? parentId, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.ListFoldersAsync(parentId, cancellationToken);

        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway, result);

        return Ok(result.Data);
    }

    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.CreateFolderAsync(request, cancellationToken);

        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway, result);

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [HttpPost("file/upload")]
    public async Task<IActionResult> UploadFile([FromForm] SubmitDocumentRequest request, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.SubmitUploadAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result.Data);
    }

    [HttpPost("file/archive")]
    public async Task<IActionResult> ArchiveFiles([FromForm] ArchiveFileRequest form, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.SubmitArchiveAsync(form, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [HttpGet("file/{fileId:long}")]
    public async Task<IActionResult> GetFile(long fileId, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.RetrieveFileAsync(fileId, cancellationToken);

        if (!result.Success)
            return NotFound(result);

        var file = result.Data!;
        return File(file.Content, file.ContentType, file.FileName ?? $"file-{fileId}");
    }
}