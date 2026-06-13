using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Configuration.Controllers;

// Class-level [Authorize] only requires authentication. Role guards live on
// each HR/admin action so the public-safe /slots/map endpoint can be reached
// by any authenticated tenant user (including employees). ASP.NET Core combines
// [Authorize] attributes additively — a more permissive action-level attribute
// does *not* relax a stricter class-level one — so the role restriction must
// be applied per-action rather than at the class level.
[ApiController]
[Authorize]
public sealed class ParkingSlotController(ParkingSlotService service, ICurrentUser currentUser) : ControllerBase
{
    private const string HrAdminRoles = $"{ConfigurationRoles.Admin},{ConfigurationRoles.HrManager}";

    [HttpGet("/configuration/locations/{locationId}/slots")]
    [Authorize(Roles = HrAdminRoles)]
    public async Task<IActionResult> GetSlots(string locationId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var slots = await service.GetByLocationAsync(currentUser.TenantId, locationId, ct);
        return Ok(slots);
    }

    [HttpPut("/configuration/locations/{locationId}/slots")]
    [Authorize(Roles = HrAdminRoles)]
    public async Task<IActionResult> PutSlots(string locationId, [FromBody] PutSlotsRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var slots = request.Slots
            .Select(s => s.ToDomain(currentUser.TenantId, locationId))
            .ToList();

        var errors = await service.ReplaceAsync(currentUser.TenantId, locationId, slots, currentUser.UserId, request.ChangeReason, ct);
        return errors.Count > 0 ? BadRequest(new { errors }) : NoContent();
    }

    [HttpGet("/configuration/locations/{locationId}/slots/history")]
    [Authorize(Roles = HrAdminRoles)]
    public async Task<IActionResult> GetSlotHistory(string locationId, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        limit = Math.Clamp(limit, 1, 100);
        var history = await service.GetChangeHistoryAsync(currentUser.TenantId, locationId, limit, ct);
        return Ok(history);
    }

    /// <summary>
    /// Public-safe parking map view of slot capacity. Reached via the class-level
    /// [Authorize] (no role guard), so any authenticated tenant user — including
    /// employees — can read this projection. Reservation is surfaced as a boolean
    /// only; ReservedForUserId is never returned here.
    /// </summary>
    [HttpGet("/configuration/locations/{locationId}/slots/map")]
    [ProducesResponseType(typeof(IReadOnlyList<SlotMapDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSlotsMap(string locationId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var slots = await service.GetByLocationAsync(currentUser.TenantId, locationId, ct);
        var map = slots.Select(s => new SlotMapDto(
            SlotId: s.SlotId,
            IsActive: s.IsActive,
            HasCharger: s.HasCharger,
            IsAccessible: s.IsAccessible,
            IsCompanyCarOnly: s.IsCompanyCarOnly,
            IsMotorcycleCapacity: s.IsMotorcycleCapacity,
            // Surface the effective unit count so the Parking Map can show "x 4" badges
            // for multi-unit motorcycle areas. Returns 1 for non-motorcycle slots.
            MotorcycleCapacityUnits: s.EffectiveMotorcycleCapacityUnits,
            IsReserved: !string.IsNullOrEmpty(s.ReservedForUserId))).ToList();

        return Ok(map);
    }
}

public sealed record PutSlotsRequest(IReadOnlyList<SlotDto> Slots, string? ChangeReason = null);

/// <summary>
/// Public-safe slot projection. ReservedForUserId is intentionally omitted —
/// only the boolean IsReserved flag is exposed to non-HR callers.
/// </summary>
public sealed record SlotMapDto(
    string SlotId,
    bool IsActive,
    bool HasCharger,
    bool IsAccessible,
    bool IsCompanyCarOnly,
    bool IsMotorcycleCapacity,
    int MotorcycleCapacityUnits,
    bool IsReserved);

public sealed record SlotDto(
    string SlotId,
    bool IsActive,
    bool HasCharger,
    bool IsAccessible,
    bool IsCompanyCarOnly,
    bool IsMotorcycleCapacity,
    string? ReservedForUserId,
    int? MotorcycleCapacityUnits = null)
{
    internal ParkingSlot ToDomain(string tenantId, string locationId) =>
        new()
        {
            SlotId = SlotId,
            TenantId = tenantId,
            LocationId = locationId,
            IsActive = IsActive,
            HasCharger = HasCharger,
            IsAccessible = IsAccessible,
            IsCompanyCarOnly = IsCompanyCarOnly,
            IsMotorcycleCapacity = IsMotorcycleCapacity,
            MotorcycleCapacityUnits = MotorcycleCapacityUnits,
            ReservedForUserId = ReservedForUserId
        };
}
