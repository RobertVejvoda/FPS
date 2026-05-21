using FPS.Customer.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
public sealed class TenantParkingBootstrapController(
    TenantParkingBootstrapService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/tenants/{tenantId}/parking-bootstrap")]
    public async Task<IActionResult> Get(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var bootstrap = await service.GetAsync(tenantId, ct);
        return Ok(new
        {
            tenantId = bootstrap.TenantId,
            defaultPolicyConfigured = bootstrap.DefaultPolicyConfigured,
            policyRecordedByHash = bootstrap.PolicyRecordedByHash,
            policyRecordedAt = bootstrap.PolicyRecordedAt,
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
    public async Task<IActionResult> RecordPolicy(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        var actorHash = HashActor(currentUser.UserId);
        var error = await service.RecordDefaultPolicyAsync(tenantId, actorHash, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpPost("/tenants/{tenantId}/parking-bootstrap/locations")]
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

public sealed record RecordLocationRequest(
    string? LocationId,
    int ActiveSlotCount,
    bool HasLocationPolicy);
