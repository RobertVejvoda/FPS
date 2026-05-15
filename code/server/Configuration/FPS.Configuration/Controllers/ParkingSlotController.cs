using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Configuration.Controllers;

[ApiController]
[Authorize(Roles = $"{ConfigurationRoles.Admin},{ConfigurationRoles.HrManager}")]
public sealed class ParkingSlotController(ParkingSlotService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/configuration/locations/{locationId}/slots")]
    public async Task<IActionResult> GetSlots(string locationId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var slots = await service.GetByLocationAsync(currentUser.TenantId, locationId, ct);
        return Ok(slots);
    }

    [HttpPut("/configuration/locations/{locationId}/slots")]
    public async Task<IActionResult> PutSlots(string locationId, [FromBody] PutSlotsRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var slots = request.Slots
            .Select(s => s.ToDomain(currentUser.TenantId, locationId))
            .ToList();

        var errors = await service.ReplaceAsync(currentUser.TenantId, locationId, slots, ct);
        return errors.Count > 0 ? BadRequest(new { errors }) : NoContent();
    }
}

public sealed record PutSlotsRequest(IReadOnlyList<SlotDto> Slots);

public sealed record SlotDto(
    string SlotId,
    bool IsActive,
    bool HasCharger,
    bool IsAccessible,
    bool IsCompanyCarOnly,
    bool IsMotorcycleCapacity,
    string? ReservedForUserId)
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
            ReservedForUserId = ReservedForUserId
        };
}
