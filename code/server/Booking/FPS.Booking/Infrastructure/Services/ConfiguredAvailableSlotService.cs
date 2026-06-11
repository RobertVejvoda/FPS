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
        var section = configuration.GetSection($"AvailableSlots:{tenantId}:{locationId}");
        var explicitSlots = section
            .GetChildren()
            .Where(s => s["SlotId"] is not null)
            .Select(CreateSlot)
            .ToList();

        if (explicitSlots.Count > 0)
            return Task.FromResult<IReadOnlyList<AvailableSlot>>(explicitSlots);

        var generatedSlots = GenerateSlots(section);
        if (generatedSlots.Count > 0)
            return Task.FromResult<IReadOnlyList<AvailableSlot>>(generatedSlots);

        return Task.FromResult<IReadOnlyList<AvailableSlot>>([]);
    }

    private static AvailableSlot CreateSlot(IConfigurationSection section) =>
        AvailableSlot.Create(
            ParkingSlotId.FromString(section["SlotId"] ?? section.Key),
            bool.TryParse(section["HasCharger"], out var c) && c,
            bool.TryParse(section["IsAccessible"], out var a) && a,
            bool.TryParse(section["IsCompanyCarReserved"], out var r) && r);

    private static List<AvailableSlot> GenerateSlots(IConfigurationSection section)
    {
        if (!int.TryParse(section["SlotCount"], out var slotCount) || slotCount <= 0)
            return [];

        var chargerCount = ReadNonNegativeInt(section, "ChargerCount");
        var accessibleCount = ReadNonNegativeInt(section, "AccessibleCount");
        var companyCarReservedCount = ReadNonNegativeInt(section, "CompanyCarReservedCount");
        var firstSlotNumber = ReadPositiveInt(section, "FirstSlotNumber", 1);

        return Enumerable.Range(1, slotCount)
            .Select(i => AvailableSlot.Create(
                ParkingSlotId.FromString((firstSlotNumber + i - 1).ToString()),
                hasCharger: i <= chargerCount,
                isAccessible: i <= accessibleCount,
                isCompanyCarReserved: i <= companyCarReservedCount))
            .ToList();
    }

    private static int ReadNonNegativeInt(IConfigurationSection section, string key) =>
        int.TryParse(section[key], out var value) && value > 0 ? value : 0;

    private static int ReadPositiveInt(IConfigurationSection section, string key, int defaultValue) =>
        int.TryParse(section[key], out var value) && value > 0 ? value : defaultValue;
}
