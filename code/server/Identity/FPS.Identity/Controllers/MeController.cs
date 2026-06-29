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
        // PLAT008A — platform-plane principals are cross-tenant: a platform-issuer token
        // carries platform_* roles and fps_platform=true but no tenant_id. They must still
        // resolve their identity so the operator console can read its roles. A tenant token
        // still requires a tenant_id (an empty-tenant customer token never reaches here with
        // platform roles — TenantClaimsTransformation strips them).
        var isPlatform = currentUser.Roles.Any(FpsRoles.IsPlatformRole);
        if (string.IsNullOrEmpty(currentUser.UserId) ||
            (string.IsNullOrEmpty(currentUser.TenantId) && !isPlatform))
            return Unauthorized();

        return Ok(new MeResponse(currentUser.UserId, currentUser.TenantId, currentUser.Roles));
    }
}
