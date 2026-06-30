using FPS.Configuration.Application;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Configuration.Controllers;

/// <summary>
/// Service-owned slot read endpoint for the Booking draw, called via Dapr service
/// invocation. Protected by <see cref="DaprInternalOnlyAttribute"/>: requires the
/// dapr-api-token header matching APP_API_TOKEN, so external callers without a Dapr
/// sidecar cannot reach it. No user JWT is involved — the caller passes the tenant
/// and location explicitly and is trusted to scope them correctly (same model as the
/// erasure endpoints). This is the full projection: unlike the public /slots/map view
/// it intentionally returns ReservedForUserId so the draw can honour company-car
/// Tier-1 fixed-slot precedence.
/// </summary>
[ApiController]
[DaprInternalOnly]
public sealed class InternalParkingSlotController(ParkingSlotService service) : ControllerBase
{
    [HttpPost("/internal/configuration/locations/slots")]
    [ProducesResponseType(typeof(IReadOnlyList<InternalSlotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlots([FromBody] InternalSlotsRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.LocationId))
            return BadRequest(new { error = "TenantId and LocationId are required." });

        var slots = await service.GetByLocationAsync(request.TenantId, request.LocationId, ct);
        var dtos = slots.Select(s => new InternalSlotDto(
            SlotId: s.SlotId,
            IsActive: s.IsActive,
            HasCharger: s.HasCharger,
            IsAccessible: s.IsAccessible,
            IsCompanyCarOnly: s.IsCompanyCarOnly,
            IsMotorcycleCapacity: s.IsMotorcycleCapacity,
            // Send the resolved unit count so the Booking draw can expand a multi-unit
            // motorcycle area into N allocatable slots without re-deriving the default.
            MotorcycleCapacityUnits: s.EffectiveMotorcycleCapacityUnits,
            ReservedForUserId: s.ReservedForUserId)).ToList();

        return Ok(dtos);
    }
}

public sealed record InternalSlotsRequest(string TenantId, string LocationId);

/// <summary>
/// Internal slot projection for the Booking draw. Carries ReservedForUserId (for
/// company-car fixed-slot precedence) and the resolved motorcycle unit count.
/// </summary>
public sealed record InternalSlotDto(
    string SlotId,
    bool IsActive,
    bool HasCharger,
    bool IsAccessible,
    bool IsCompanyCarOnly,
    bool IsMotorcycleCapacity,
    int MotorcycleCapacityUnits,
    string? ReservedForUserId);
