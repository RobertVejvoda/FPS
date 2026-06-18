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
    public async Task GetAvailableSlots_ReservedForUserId_IsCarriedToAvailableSlot()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AvailableSlots:tenant-1:loc-1:0:SlotId"] = "CC1",
                ["AvailableSlots:tenant-1:loc-1:0:ReservedForUserId"] = "user-123",
            })
            .Build();
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        var slot = Assert.Single(slots);
        Assert.Equal("user-123", slot.ReservedForUserId);
        Assert.True(slot.IsCompanyCarReserved);
    }

    [Fact]
    public async Task GetAvailableSlots_IsActive_False_ParsedCorrectly()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AvailableSlots:tenant-1:loc-1:0:SlotId"] = "A1",
                ["AvailableSlots:tenant-1:loc-1:0:IsActive"] = "false",
            })
            .Build();
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.False(slots.Single().IsActive);
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

    [Fact]
    public async Task GetAvailableSlots_WithGeneratedCount_ReturnsNumberedSlots()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AvailableSlots:tenant-1:loc-1:SlotCount"] = "3",
                ["AvailableSlots:tenant-1:loc-1:FirstSlotNumber"] = "301",
                ["AvailableSlots:tenant-1:loc-1:ChargerCount"] = "1",
                ["AvailableSlots:tenant-1:loc-1:AccessibleCount"] = "1",
            })
            .Build();
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.Equal(["301", "302", "303"], slots.Select(s => s.SlotId.Value));
        Assert.True(slots[0].HasCharger);
        Assert.True(slots[0].IsAccessible);
        Assert.False(slots[1].HasCharger);
    }

    // ── Motorcycle multi-unit expansion (CAP-468) ─────────────────────────────

    [Fact]
    public async Task GetAvailableSlots_MotorcycleSlot_DefaultsToFourUnits()
    {
        // A motorcycle-marked slot with no explicit unit count expands to 4 logical
        // AvailableSlot instances with the documented "{slotId}-{n}" id suffix.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AvailableSlots:tenant-1:loc-1:0:SlotId"] = "M1",
                ["AvailableSlots:tenant-1:loc-1:0:IsMotorcycleCapacity"] = "true",
            })
            .Build();
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.Equal(4, slots.Count);
        Assert.All(slots, s => Assert.True(s.IsMotorcycleCapacity));
        Assert.Equal(["M1-1", "M1-2", "M1-3", "M1-4"], slots.Select(s => s.SlotId.Value));
    }

    [Fact]
    public async Task GetAvailableSlots_MotorcycleSlot_RespectsConfiguredUnitCount()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AvailableSlots:tenant-1:loc-1:0:SlotId"] = "M1",
                ["AvailableSlots:tenant-1:loc-1:0:IsMotorcycleCapacity"] = "true",
                ["AvailableSlots:tenant-1:loc-1:0:MotorcycleCapacityUnits"] = "2",
            })
            .Build();
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.Equal(2, slots.Count);
        Assert.Equal(["M1-1", "M1-2"], slots.Select(s => s.SlotId.Value));
    }

    [Fact]
    public async Task GetAvailableSlots_NonMotorcycleSlot_NotExpanded()
    {
        // Even if MotorcycleCapacityUnits is set on a non-motorcycle slot, it must
        // not be expanded — the flag is ignored when isMotorcycleCapacity=false.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AvailableSlots:tenant-1:loc-1:0:SlotId"] = "A1",
                ["AvailableSlots:tenant-1:loc-1:0:MotorcycleCapacityUnits"] = "5",
            })
            .Build();
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.Single(slots);
        Assert.Equal("A1", slots[0].SlotId.Value);
        Assert.False(slots[0].IsMotorcycleCapacity);
    }

    [Fact]
    public async Task GetAvailableSlots_SingleUnitMotorcycleSlot_KeepsOriginalSlotId()
    {
        // MotorcycleCapacityUnits=1 stays as one AvailableSlot with the original id —
        // no "-1" suffix when there's only one unit, so single-bike areas don't get
        // a confusing suffix in allocation records.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AvailableSlots:tenant-1:loc-1:0:SlotId"] = "M1",
                ["AvailableSlots:tenant-1:loc-1:0:IsMotorcycleCapacity"] = "true",
                ["AvailableSlots:tenant-1:loc-1:0:MotorcycleCapacityUnits"] = "1",
            })
            .Build();
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        Assert.Single(slots);
        Assert.Equal("M1", slots[0].SlotId.Value);
        Assert.True(slots[0].IsMotorcycleCapacity);
    }

    [Fact]
    public async Task GetAvailableSlots_GeneratedCount_ExpandsMotorcycleSlots()
    {
        // SlotCount with MotorcycleCount + MotorcycleCapacityUnits expands the
        // first N slots into motorcycle units.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AvailableSlots:tenant-1:loc-1:SlotCount"] = "3",
                ["AvailableSlots:tenant-1:loc-1:FirstSlotNumber"] = "501",
                ["AvailableSlots:tenant-1:loc-1:MotorcycleCount"] = "1",
                ["AvailableSlots:tenant-1:loc-1:MotorcycleCapacityUnits"] = "3",
            })
            .Build();
        var sut = new ConfiguredAvailableSlotService(config);

        var slots = await sut.GetAvailableSlotsAsync("tenant-1", "loc-1", Date, Slot9To17);

        // 1 motorcycle slot × 3 units + 2 normal slots = 5 logical AvailableSlots
        Assert.Equal(5, slots.Count);
        Assert.Equal(["501-1", "501-2", "501-3", "502", "503"], slots.Select(s => s.SlotId.Value));
        Assert.True(slots[0].IsMotorcycleCapacity);
        Assert.False(slots[3].IsMotorcycleCapacity);
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
