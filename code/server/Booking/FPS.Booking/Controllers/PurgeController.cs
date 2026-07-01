using FPS.Booking.Infrastructure;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.Controllers;

/// <summary>
/// Service-owned destructive tenant-purge endpoint (PLAT003C) called by the platform purge
/// orchestrator via Dapr service invocation. Protected by DaprInternalOnly: requires the
/// dapr-api-token header matching APP_API_TOKEN, so external callers cannot reach it in production.
/// </summary>
[ApiController]
[DaprInternalOnly]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PurgeController(BookingTenantStorePurger purger) : ControllerBase
{
    [HttpPost("/purge/tenant")]
    public async Task<IActionResult> PurgeTenant([FromBody] TenantPurgeRequest req, CancellationToken ct)
    {
        TenantPurgeScope scope;
        try
        {
            scope = TenantPurgeScope.For(req.TenantId);
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }

        var count = await purger.PurgeAsync(scope, req.SandboxReset, ct);
        return Ok(new TenantPurgeResponse(purger.Service, count));
    }
}
