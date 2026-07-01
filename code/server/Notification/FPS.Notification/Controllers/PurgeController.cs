using FPS.Notification.Infrastructure;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Notification.Controllers;

/// <summary>
/// Internal single-tenant purge endpoint (PLAT003C), invoked by the Customer plane's
/// TenantPurgeOrchestrator over Dapr service invocation. DESTRUCTIVE: removes all of a tenant's
/// notification data. Protected by DaprInternalOnly — requires the Dapr app API token, so external
/// callers without a sidecar cannot reach it in production.
/// </summary>
[ApiController]
[DaprInternalOnly]
public sealed class PurgeController(NotificationTenantStorePurger purger) : ControllerBase
{
    [HttpPost("/purge/tenant")]
    public async Task<IActionResult> PurgeTenant([FromBody] TenantPurgeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        TenantPurgeScope scope;
        try
        {
            scope = TenantPurgeScope.For(request.TenantId);
        }
        catch (ArgumentException ex)
        {
            // Fail closed on a blank / contract-invalid tenant id rather than purging nothing silently.
            return BadRequest(ex.Message);
        }

        var count = await purger.PurgeAsync(scope, request.SandboxReset, ct);
        return Ok(new TenantPurgeResponse(purger.Service, count));
    }
}
