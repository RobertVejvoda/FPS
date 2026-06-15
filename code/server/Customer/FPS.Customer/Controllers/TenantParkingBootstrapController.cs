using FPS.Customer.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

// Class-level [Authorize] only requires authentication. The read-only GET
// is open to both admin and hr_manager so HR can discover the tenant's
// known locations from the Configuration page (issue #477) — same role
// policy the Configuration location/slot endpoints already use. The
// mutating POSTs stay admin-only because they record tenant-wide
// bootstrap state. Same lesson as MAP001 (#467) and HR display-names
// (#475) — controller-level role attributes are additive, so the
// per-action relaxation has to live on the methods.
[ApiController]
[Authorize]
public sealed class TenantParkingBootstrapController(
    TenantParkingBootstrapService service,
    ICurrentUser currentUser) : ControllerBase
{
    private const string DiscoveryRoles = "admin,hr_manager";
    private const string MutatingRoles = "admin";

    [HttpGet("/tenants/{tenantId}/parking-bootstrap")]
    [Authorize(Roles = DiscoveryRoles)]
    public async Task<IActionResult> Get(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var bootstrap = await service.GetAsync(tenantId, ct);
        var snap = bootstrap.PolicySnapshot;
        return Ok(new
        {
            tenantId = bootstrap.TenantId,
            defaultPolicyConfigured = bootstrap.DefaultPolicyConfigured,
            policySnapshot = snap is null ? null : new
            {
                timeZone = snap.TimeZone,
                drawCutOffTime = snap.DrawCutOffTime,
                dailyRequestCap = snap.DailyRequestCap,
                allocationLookbackDays = snap.AllocationLookbackDays,
                recordedByHash = snap.RecordedByHash,
                recordedAt = snap.RecordedAt,
            },
            hasUsableLocation = bootstrap.HasUsableLocation,
            isComplete = bootstrap.IsComplete,
            locations = bootstrap.Locations.Select(l => new
            {
                locationId = l.LocationId,
                activeSlotCount = l.ActiveSlotCount,
                hasLocationPolicy = l.HasLocationPolicy,
                isUsable = l.IsUsable,
                recordedByHash = l.RecordedByHash,
                recordedAt = l.RecordedAt,
            }),
        });
    }

    [HttpPost("/tenants/{tenantId}/parking-bootstrap/policy")]
    [Authorize(Roles = MutatingRoles)]
    public async Task<IActionResult> RecordPolicy(
        string tenantId, [FromBody] RecordPolicyRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        var actorHash = HashActor(currentUser.UserId);
        var error = await service.RecordDefaultPolicyAsync(
            tenantId,
            request.TimeZone ?? string.Empty,
            request.DrawCutOffTime ?? string.Empty,
            request.DailyRequestCap,
            request.AllocationLookbackDays,
            actorHash, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpPost("/tenants/{tenantId}/parking-bootstrap/locations")]
    [Authorize(Roles = MutatingRoles)]
    public async Task<IActionResult> RecordLocation(
        string tenantId, [FromBody] RecordLocationRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        var actorHash = HashActor(currentUser.UserId);
        var error = await service.RecordLocationAsync(
            tenantId, request.LocationId ?? string.Empty,
            request.ActiveSlotCount, request.HasLocationPolicy,
            actorHash, ct);

        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    private static string HashActor(string userId) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(userId)))[..16];
}

public sealed record RecordPolicyRequest(
    string? TimeZone,
    string? DrawCutOffTime,
    int DailyRequestCap,
    int AllocationLookbackDays);

public sealed record RecordLocationRequest(
    string? LocationId,
    int ActiveSlotCount,
    bool HasLocationPolicy);
