using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Configuration.Controllers;

/// <summary>
/// Service-owned destructive tenant-purge endpoint (PLAT003C) called by the platform purge
/// orchestrator via Dapr service invocation. Protected by DaprInternalOnly: requires the
/// dapr-api-token header matching APP_API_TOKEN, so external callers cannot reach it in production.
/// </summary>
[ApiController]
[DaprInternalOnly]
public sealed class PurgeController(ConfigurationTenantStorePurger purger) : ControllerBase
{
    [HttpPost("/purge/tenant")]
    public async Task<IActionResult> PurgeTenant([FromBody] TenantPurgeRequest request, CancellationToken ct)
    {
        TenantPurgeScope scope;
        try
        {
            scope = TenantPurgeScope.For(request.TenantId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var count = await purger.PurgeAsync(scope, request.SandboxReset, ct);
        return Ok(new TenantPurgeResponse(purger.Service, count));
    }
}
