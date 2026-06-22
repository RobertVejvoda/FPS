using FPS.Customer.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class TenantDiscoveryController(TenantService service) : ControllerBase
{
    [HttpGet("/tenants/discover")]
    public async Task<IActionResult> Discover([FromQuery] string? domain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { error = "Query parameter 'domain' is required." });

        var result = await service.DiscoverAsync(domain, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
