using FPS.Customer.Application;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

/// <summary>
/// PLAT-seats (#710) — internal, Dapr-only lookup of a tenant's enabled modules. Used by the
/// Booking service to enforce that a Seats request is only accepted for a tenant that has the Seats
/// module enabled (the module boundary must hold on the server, not just the web nav). Protected by
/// <see cref="DaprInternalOnlyAttribute"/>; excluded from the open OpenAPI client.
/// </summary>
[ApiController]
[DaprInternalOnly]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class InternalTenantModulesController(TenantService service) : ControllerBase
{
    [HttpPost("/internal/customer/tenant-modules")]
    public async Task<IActionResult> GetModules([FromBody] InternalTenantModulesRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            return BadRequest(new { error = "TenantId is required." });

        try
        {
            var tenant = await service.GetAsync(request.TenantId, ct);
            var modules = tenant is null ? [] : tenant.EnabledModules.Select(m => m.ToString()).ToList();
            return Ok(new InternalTenantModulesResponse(modules));
        }
        catch (ArgumentException)
        {
            // Invalid tenant id shape → no modules (Booking fails the seats gate closed).
            return Ok(new InternalTenantModulesResponse([]));
        }
    }
}

public sealed record InternalTenantModulesRequest(string TenantId);
public sealed record InternalTenantModulesResponse(IReadOnlyList<string> EnabledModules);
