using FPS.Customer.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
public sealed class TenantDemoSeedController(
    TenantDemoSeedService seedService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Seeds the canonical Green Logistics demo dataset into a Sandbox or Evaluation
    /// tenant. Idempotent — re-running replaces the demo profiles and configuration.
    /// Rejected for Production tenants.
    /// </summary>
    [HttpPost("/tenants/{tenantId}/demo-seed")]
    public async Task<IActionResult> DemoSeed(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var authorizationHeader = HttpContext.Request.Headers.Authorization.ToString();
        var (result, error) = await seedService.SeedAsync(tenantId, currentUser.UserId, authorizationHeader, ct);

        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return Ok(result);
    }
}
