using Dapr.Client;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FPS.Booking.Infrastructure.Services;

// Authoritative slot source for booking submission and the Draw: the Configuration
// service, read over Dapr service invocation. This replaces the Phase 1 appsettings
// stub as the primary source so the curated per-location slot layout — and, crucially,
// company-car per-user reservations (ReservedForUserId) — drive allocation (#665).
//
// Falls back to the appsettings-backed ConfiguredAvailableSlotService when Configuration
// returns no slots for the location, or when the call fails, so local/demo profiles and
// the draw stay resilient during the transition.
public sealed class ConfigurationSlotService : IAvailableSlotService
{
    private const string ConfigurationAppId = "fairspot-configuration";
    private const string SlotsMethod = "internal/configuration/locations/slots";

    private readonly DaprClient daprClient;
    private readonly ConfiguredAvailableSlotService fallback;
    private readonly ILogger<ConfigurationSlotService> logger;

    public ConfigurationSlotService(
        DaprClient daprClient,
        ConfiguredAvailableSlotService fallback,
        ILogger<ConfigurationSlotService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(logger);
        this.daprClient = daprClient;
        this.fallback = fallback;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<AvailableSlot>> GetAvailableSlotsAsync(
        string tenantId,
        string locationId,
        DateOnly date,
        TimeSlot timeSlot,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ConfigurationSlot>? configurationSlots = null;
        try
        {
            configurationSlots = await daprClient.InvokeMethodAsync<InternalSlotsRequest, IReadOnlyList<ConfigurationSlot>>(
                ConfigurationAppId,
                SlotsMethod,
                new InternalSlotsRequest(tenantId, locationId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Configuration slot lookup failed for tenant {TenantId} location {LocationId}; falling back to configured slots.",
                tenantId,
                locationId);
        }

        if (configurationSlots is { Count: > 0 })
            return ProjectSlots(configurationSlots);

        return await fallback.GetAvailableSlotsAsync(tenantId, locationId, date, timeSlot, cancellationToken);
    }

    // Project the Configuration slots into Draw-ready AvailableSlots. Exposed for unit
    // testing the mapping in isolation from the Dapr transport.
    public static IReadOnlyList<AvailableSlot> ProjectSlots(IEnumerable<ConfigurationSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        return slots.SelectMany(MapToUnits).ToList();
    }

    // Expand a Configuration slot into one AvailableSlot per allocatable unit. Motorcycle
    // areas with N>1 units become N slots ("{slotId}-{unit}"); every other slot maps 1:1
    // so existing allocation references stay stable. Mirrors the appsettings expansion.
    private static IEnumerable<AvailableSlot> MapToUnits(ConfigurationSlot slot)
    {
        var units = slot.IsMotorcycleCapacity && slot.MotorcycleCapacityUnits > 0
            ? slot.MotorcycleCapacityUnits
            : 1;

        if (units <= 1)
        {
            yield return AvailableSlot.Create(
                ParkingSlotId.FromString(slot.SlotId),
                slot.IsActive, slot.HasCharger, slot.IsAccessible,
                slot.IsCompanyCarOnly, slot.ReservedForUserId, slot.IsMotorcycleCapacity);
            yield break;
        }

        for (var unit = 1; unit <= units; unit++)
        {
            yield return AvailableSlot.Create(
                ParkingSlotId.FromString($"{slot.SlotId}-{unit}"),
                slot.IsActive, slot.HasCharger, slot.IsAccessible,
                slot.IsCompanyCarOnly, slot.ReservedForUserId, slot.IsMotorcycleCapacity);
        }
    }
}

// Mirror of the Configuration service's internal slot contract (see
// FPS.Configuration InternalParkingSlotController). Duplicated rather than shared,
// matching the erasure service-invocation convention.
public sealed record InternalSlotsRequest(string TenantId, string LocationId);

public sealed record ConfigurationSlot(
    string SlotId,
    bool IsActive,
    bool HasCharger,
    bool IsAccessible,
    bool IsCompanyCarOnly,
    bool IsMotorcycleCapacity,
    int MotorcycleCapacityUnits,
    string? ReservedForUserId);
