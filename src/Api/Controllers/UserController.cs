using Microsoft.AspNetCore.Mvc;

using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// Get the profile of a citizen user by their Keycloak user ID.
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile([FromQuery] Guid keycloakUserId)
    {
        var result = await _userService.GetUserProfileAsync(keycloakUserId);

        if (!result.Success)
            return Accepted(result);

        return Ok(result);
    }
}
