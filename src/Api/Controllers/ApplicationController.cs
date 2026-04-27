using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

using CitizenPortal.Application.Interfaces;
using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Api.Controllers;

[ApiController]
[Route("api/citizen/applications")]
//[Authorize(Roles = "citizen")]
public class ApplicationController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    /// Submit a new application with optional file attachments.
    /// Citizen must already be provisioned (via oauth2callback on login).
    /// Files are uploaded to DMS.Storage, then the application + outbox event
    /// are saved in a single DB transaction (Outbox Pattern).
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitApplication(
        [FromForm] ApplicationCreateDto request,
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.SubmitApplicationAsync(request, files, cancellationToken);

        if (!result.Success)
            return Accepted(result);

        return Ok(result);
    }

    /// Get a specific application by its public tracking ID.

    [HttpGet("{publicId:guid}")]
    public async Task<IActionResult> GetApplication(Guid publicId)
    {      
        var result = await _applicationService.GetApplicationAsync(publicId);

        if (!result.Success)
            return Accepted(result);

        return Ok(result);
    }

    /// <summary>
    /// List all applications for the authenticated citizen (paged).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetApplications([FromQuery] ApplicationQueryParams request)
    {     
         var result = await _applicationService.GetApplicationsAsync(request);

        if (!result.Success)
            return Accepted(result);

        return Ok(result);
    }   
}
