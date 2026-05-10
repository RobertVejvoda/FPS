using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Identity.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        userId = currentUser.UserId,
        tenantId = currentUser.TenantId,
        roles = currentUser.Roles
    });
}
