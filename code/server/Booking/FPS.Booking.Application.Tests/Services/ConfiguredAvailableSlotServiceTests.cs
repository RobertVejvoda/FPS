using FPS.Booking.Domain.ValueObjects;
using FPS.Booking.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace FPS.Booking.Application.Tests.Services;

public sealed class ConfiguredAvailableSlotServiceTests
{
    private static readonly DateOnly Date = new(2026, 6, 2);
    private static readonly TimeSlot Slot9To17 = TimeSlot.Create(
        new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task GetAvailableSlots_WithConfiguredSlots_ReturnsThem()
    {
        var config = BuildConfig("tenant-1", "loc-1", new[]
        {
            ("A1", false, false, false),
            ("A2", true, false, false)
        });
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public async Task GetAvailableSlots_ChargerFlag_ParsedCorrectly()
    {
        var config = BuildConfig("tenant-1", "loc-1", new[] { ("EV1", true, false, false) });
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.True(slots.Single().HasCharger);
    }

    [Fact]
    public async Task GetAvailableSlots_CompanyCarReservedFlag_ParsedCorrectly()
    {
        var config = BuildConfig("tenant-1", "loc-1", new[] { ("CC1", false, false, true) });
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.True(slots.Single().IsCompanyCarReserved);
    }

    [Fact]
    public async Task GetAvailableSlots_NoConfig_ReturnsEmpty()
    {
        var config = new ConfigurationBuilder().Build();
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetAvailableSlots_DifferentTenant_ReturnsEmpty()
    {
        var config = BuildConfig("tenant-A", "loc-1", new[] { ("A1", false, false, false) });
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-B", "loc-1", Date, Slot9To17);

        Assert.Empty(slots);
    }

    private static IConfiguration BuildConfig(
        string tenantId, string locationId,
        IEnumerable<(string SlotId, bool HasCharger, bool IsAccessible, bool IsCompanyCarReserved)> slots)
    {
        var dict = new Dictionary<string, string?>();
        var i = 0;
        foreach (var (slotId, hasCharger, isAccessible, isCompanyCar) in slots)
        {
            var prefix = $"AvailableSlots:{tenantId}:{locationId}:{i}";
            dict[$"{prefix}:SlotId"] = slotId;
            dict[$"{prefix}:HasCharger"] = hasCharger.ToString();
            dict[$"{prefix}:IsAccessible"] = isAccessible.ToString();
            dict[$"{prefix}:IsCompanyCarReserved"] = isCompanyCar.ToString();
            i++;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }
}
