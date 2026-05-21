using FPS.Customer.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
public sealed class TenantReadinessController(
    TenantReadinessService readinessService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/tenants/{tenantId}/readiness")]
    public async Task<IActionResult> Check(
        string tenantId,
        [FromQuery] bool dryRun = false,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var (report, error) = await readinessService.CheckAsync(tenantId, dryRun, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });

        return Ok(ToResponse(report!));
    }

    private static ReadinessReportResponse ToResponse(ReadinessReport r) => new(
        r.TenantId,
        r.IsDryRun,
        r.IsReady,
        r.Checks.Select(c => new ReadinessCheckDto(
            c.Name,
            c.Status.ToString(),
            string.IsNullOrEmpty(c.Reason) ? null : c.Reason)).ToList());
}

public sealed record ReadinessCheckDto(string Name, string Status, string? Reason);

public sealed record ReadinessReportResponse(
    string TenantId,
    bool IsDryRun,
    bool IsReady,
    IReadOnlyList<ReadinessCheckDto> Checks);
