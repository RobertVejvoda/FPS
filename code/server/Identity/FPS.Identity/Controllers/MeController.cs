using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Identity.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public sealed class MeController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        if (string.IsNullOrEmpty(currentUser.UserId) || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        return Ok(new
        {
            userId = currentUser.UserId,
            tenantId = currentUser.TenantId,
            roles = currentUser.Roles
        });
    }
}
