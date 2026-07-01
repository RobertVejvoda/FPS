using FPS.Audit.Infrastructure;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Audit.Controllers;

/// <summary>
/// Service-owned tenant-purge endpoint invoked by the platform purge orchestrator (Customer) via
/// Dapr service invocation. Protected by DaprInternalOnly: requires the dapr-api-token header.
/// Audit holds immutable evidence, so the purger self-gates and only clears data on a sandbox reset.
/// </summary>
[ApiController]
[DaprInternalOnly]
public sealed class PurgeController(AuditTenantStorePurger purger) : ControllerBase
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
