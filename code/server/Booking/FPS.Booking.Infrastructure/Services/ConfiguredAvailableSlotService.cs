using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace FPS.Booking.Infrastructure.Services;

// Phase 1 stub — returns slots from IConfiguration under "AvailableSlots".
// Real implementation will call the Facility service via Dapr service invocation.
public sealed class ConfiguredAvailableSlotService : IAvailableSlotService
{
    private readonly IConfiguration configuration;

    public ConfiguredAvailableSlotService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this.configuration = configuration;
    }

    public Task<IReadOnlyList<AvailableSlot>> GetAvailableSlotsAsync(
        string tenantId,
        string locationId,
        DateOnly date,
        TimeSlot timeSlot,
        CancellationToken cancellationToken = default)
    {
        var slots = configuration
            .GetSection($"AvailableSlots:{tenantId}:{locationId}")
            .GetChildren()
            .Select(s => AvailableSlot.Create(
                ParkingSlotId.FromString(s["SlotId"] ?? s.Key),
                bool.TryParse(s["HasCharger"], out var c) && c,
                bool.TryParse(s["IsAccessible"], out var a) && a,
                bool.TryParse(s["IsCompanyCarReserved"], out var r) && r))
            .ToList();

        return Task.FromResult<IReadOnlyList<AvailableSlot>>(slots);
    }
}
