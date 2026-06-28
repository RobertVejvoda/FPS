using FPS.Customer.Identity;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FPS.Customer.Application;

namespace FPS.Customer.Controllers;

// PLAT001 — the read-only GET is also open to hr_manager (own tenant only) so the
// Configuration page can discover locations (issue #477). The mutating POSTs are
// tenant-admin only ([RequireTenantAdmin]). Role-list attributes are avoided here
// because they would exclude a cross-tenant platform_admin; the helper checks
// (CanAdministerTenant) carry the platform/tenant logic instead.
[ApiController]
[Authorize]
public sealed class TenantParkingBootstrapController(
    TenantParkingBootstrapService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/tenants/{tenantId}/parking-bootstrap")]
    public async Task<IActionResult> Get(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        // A tenant admin or platform_admin may read this (CanAdministerTenant).
        // hr_manager may read it only for their own tenant. No actor may read
        // another tenant's bootstrap.
        var hrOwnTenant = currentUser.IsInRole(FpsRoles.HrManager)
            && string.Equals(tenantId, currentUser.TenantId, StringComparison.Ordinal);
        if (!currentUser.CanAdministerTenant(tenantId) && !hrOwnTenant)
        {
            return Forbid();
        }

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
    [RequireTenantAdmin]
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
    [RequireTenantAdmin]
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
