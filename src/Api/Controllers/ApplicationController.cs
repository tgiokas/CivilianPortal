using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

using CitizenPortal.Application.Interfaces;
using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ApplicationController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    /// Submit a new application with optional file attachments.
    /// Files are uploaded to DMS.Storage, then the application + outbox event
    /// are saved in a single DB transaction (Outbox Pattern).
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitApplication(
        [FromForm] ApplicationCreateDto request,
        [FromForm] List<IFormFile>? files, 
        [FromForm] string externalSystem,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.SubmitApplicationAsync(request, files, externalSystem, cancellationToken);

        if (!result.Success)
            return Accepted(result);

        return Ok(result);
    }

    /// Get a specific application by its public tracking ID.
    [HttpGet("getApp")]
    public async Task<IActionResult> GetApplication([FromQuery] Guid publicId)
    {
        //var keycloakUserId = GetKeycloakUserId();
        //if (keycloakUserId == Guid.Empty)
        //    return Unauthorized();

        var result = await _applicationService.GetApplicationAsync(publicId);

        if (!result.Success)
            return Accepted(result);

        return Ok(result);
    }

    /// List all applications for a user.
    [HttpGet("getUserApps")]
    public async Task<IActionResult> GetApplications([FromQuery] Guid userId)
    {     
         var result = await _applicationService.GetUserApplicationsAsync(userId);

        if (!result.Success)
            return Accepted(result);

        return Ok(result);
    }

    private Guid GetKeycloakUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
