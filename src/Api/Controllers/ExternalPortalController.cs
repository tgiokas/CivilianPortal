using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using CitizenPortal.Api.Filters;
using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Api.Controllers;

/// OPS integration surface: obtain a client-credentials token anonymously, then call the
/// remaining endpoints with <c>Authorization: Bearer &lt;access_token&gt;</c> (azp =
/// ops-integration-client). Citizen JWTs are rejected.
//[Authorize(Policy = "OpsExternalPortal")]
[ApiController]
[Route("[controller]")]
public class ExternalPortalController : ControllerBase
{
    private readonly IExternalPortalService _externalPortalService;
    private readonly IAuthenticationService _authenticationService;

    public ExternalPortalController(IExternalPortalService externalPortalService, IAuthenticationService authenticationService)
    {
        _externalPortalService = externalPortalService;
        _authenticationService = authenticationService;
    }

    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> GetOpsToken([FromBody] OpsTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            return BadRequest(Result<string>.Fail("clientId and clientSecret are required."));
        }

        var result = await _authenticationService.GetOpsAccessTokenAsync(request.ClientId, request.ClientSecret);
        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
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
    [TypeFilter(typeof(FileSizeLimitFilter))]
    public async Task<IActionResult> UploadFile([FromForm] SubmitDocumentRequest request, CancellationToken cancellationToken)
    {
        var result = await _externalPortalService.SubmitUploadAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result.Data);
    }

    [HttpPost("file/archive")]
    [TypeFilter(typeof(FileSizeLimitFilter))]
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