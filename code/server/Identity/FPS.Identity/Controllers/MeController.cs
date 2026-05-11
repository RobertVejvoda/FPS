using FPS.Identity.Models;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Identity.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public sealed class MeController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet(Name = "GetCurrentUser")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Get()
    {
        if (string.IsNullOrEmpty(currentUser.UserId) || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        return Ok(new MeResponse(currentUser.UserId, currentUser.TenantId, currentUser.Roles));
    }
}
