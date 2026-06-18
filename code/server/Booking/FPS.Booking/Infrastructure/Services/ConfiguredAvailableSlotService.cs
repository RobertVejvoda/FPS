using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace FPS.Booking.Infrastructure.Services;

// Phase 1 stub — returns slots from IConfiguration under "AvailableSlots".
// Real implementation will call the Facility service via Dapr service invocation.
public sealed class ConfiguredAvailableSlotService : IAvailableSlotService
{
    // Per #468 v1 product rule: motorcycle-specific slots hold up to 4 bikes
    // by default, configurable per slot/area via MotorcycleCapacityUnits.
    public const int DefaultMotorcycleCapacityUnits = 4;

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
        var section = configuration.GetSection($"AvailableSlots:{tenantId}:{locationId}");
        var explicitSlots = section
            .GetChildren()
            .Where(s => s["SlotId"] is not null)
            .SelectMany(CreateSlots)
            .ToList();

        if (explicitSlots.Count > 0)
            return Task.FromResult<IReadOnlyList<AvailableSlot>>(explicitSlots);

        var generatedSlots = GenerateSlots(section);
        if (generatedSlots.Count > 0)
            return Task.FromResult<IReadOnlyList<AvailableSlot>>(generatedSlots);

        return Task.FromResult<IReadOnlyList<AvailableSlot>>([]);
    }

    private static IEnumerable<AvailableSlot> CreateSlots(IConfigurationSection section)
    {
        var rawSlotId = section["SlotId"] ?? section.Key;
        var isActive = !bool.TryParse(section["IsActive"], out var active) || active;
        var hasCharger = bool.TryParse(section["HasCharger"], out var c) && c;
        var isAccessible = bool.TryParse(section["IsAccessible"], out var a) && a;
        var legacyCompanyCarReserved = bool.TryParse(section["IsCompanyCarReserved"], out var legacyReserved) && legacyReserved;
        var isCompanyCarOnly = bool.TryParse(section["IsCompanyCarOnly"], out var companyCarOnly) && companyCarOnly;
        var reservedForUserId = section["ReservedForUserId"];
        var hasReservedOwner = !string.IsNullOrWhiteSpace(reservedForUserId);
        var isCompanyCarReserved = legacyCompanyCarReserved || isCompanyCarOnly || hasReservedOwner;
        var isMotorcycleCapacity = bool.TryParse(section["IsMotorcycleCapacity"], out var m) && m;
        var configuredUnits = int.TryParse(section["MotorcycleCapacityUnits"], out var u) && u > 0
            ? u
            : (int?)null;

        return ExpandToUnits(rawSlotId, isActive, hasCharger, isAccessible, isCompanyCarReserved,
            reservedForUserId, isMotorcycleCapacity, configuredUnits);
    }

    private static List<AvailableSlot> GenerateSlots(IConfigurationSection section)
    {
        if (!int.TryParse(section["SlotCount"], out var slotCount) || slotCount <= 0)
            return [];

        var chargerCount = ReadNonNegativeInt(section, "ChargerCount");
        var accessibleCount = ReadNonNegativeInt(section, "AccessibleCount");
        var companyCarReservedCount = ReadNonNegativeInt(section, "CompanyCarReservedCount");
        var motorcycleCount = ReadNonNegativeInt(section, "MotorcycleCount");
        var motorcycleUnitsPerSlot = int.TryParse(section["MotorcycleCapacityUnits"], out var u) && u > 0
            ? u
            : (int?)null;
        var firstSlotNumber = ReadPositiveInt(section, "FirstSlotNumber", 1);

        return Enumerable.Range(1, slotCount)
            .SelectMany(i =>
            {
                var rawSlotId = (firstSlotNumber + i - 1).ToString();
                return ExpandToUnits(
                    rawSlotId,
                    isActive: true,
                    hasCharger: i <= chargerCount,
                    isAccessible: i <= accessibleCount,
                    isCompanyCarReserved: i <= companyCarReservedCount,
                    reservedForUserId: null,
                    isMotorcycleCapacity: i <= motorcycleCount,
                    configuredUnits: motorcycleUnitsPerSlot);
            })
            .ToList();
    }

    // Expand a single configured slot row into one AvailableSlot per allocatable unit.
    // Motorcycle-specific slots get DefaultMotorcycleCapacityUnits units when no count
    // is configured. Non-motorcycle slots and motorcycle slots configured with 1 unit
    // are returned as a single AvailableSlot with the original SlotId, so existing
    // allocation references stay stable.
    private static IEnumerable<AvailableSlot> ExpandToUnits(
        string rawSlotId,
        bool isActive,
        bool hasCharger,
        bool isAccessible,
        bool isCompanyCarReserved,
        string? reservedForUserId,
        bool isMotorcycleCapacity,
        int? configuredUnits)
    {
        var units = !isMotorcycleCapacity ? 1
            : configuredUnits ?? DefaultMotorcycleCapacityUnits;

        if (units <= 1)
        {
            yield return AvailableSlot.Create(
                ParkingSlotId.FromString(rawSlotId),
                isActive, hasCharger, isAccessible, isCompanyCarReserved, reservedForUserId, isMotorcycleCapacity);
            yield break;
        }

        for (var unit = 1; unit <= units; unit++)
        {
            yield return AvailableSlot.Create(
                ParkingSlotId.FromString($"{rawSlotId}-{unit}"),
                isActive, hasCharger, isAccessible, isCompanyCarReserved, reservedForUserId, isMotorcycleCapacity);
        }
    }

    private static int ReadNonNegativeInt(IConfigurationSection section, string key) =>
        int.TryParse(section[key], out var value) && value > 0 ? value : 0;

    private static int ReadPositiveInt(IConfigurationSection section, string key, int defaultValue) =>
        int.TryParse(section[key], out var value) && value > 0 ? value : defaultValue;
}
