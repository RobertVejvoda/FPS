using FPS.Booking.Infrastructure.Services;

namespace FPS.Booking.Infrastructure.Tests.Services;

// Covers the projection of Configuration-service slots into Draw-ready AvailableSlots.
// The Dapr transport and appsettings fallback are exercised by the live seed→draw gate.
public sealed class ConfigurationSlotServiceTests
{
    [Fact]
    public void ProjectSlots_CarriesReservedForUserId_ForCompanyCarFixedSlot()
    {
        var slots = ConfigurationSlotService.ProjectSlots(new[]
        {
            new ConfigurationSlot("VIP-01", IsActive: true, HasCharger: false, IsAccessible: false,
                IsCompanyCarOnly: true, IsMotorcycleCapacity: false, MotorcycleCapacityUnits: 1, ReservedForUserId: "emp-7"),
        });

        var slot = Assert.Single(slots);
        Assert.Equal("VIP-01", slot.SlotId.Value);
        Assert.Equal("emp-7", slot.ReservedForUserId);
        Assert.True(slot.IsCompanyCarReserved);
    }

    [Fact]
    public void ProjectSlots_MapsFlags()
    {
        var slots = ConfigurationSlotService.ProjectSlots(new[]
        {
            new ConfigurationSlot("EV-01", IsActive: true, HasCharger: true, IsAccessible: false,
                IsCompanyCarOnly: false, IsMotorcycleCapacity: false, MotorcycleCapacityUnits: 1, ReservedForUserId: null),
            new ConfigurationSlot("ACC-01", IsActive: true, HasCharger: false, IsAccessible: true,
                IsCompanyCarOnly: false, IsMotorcycleCapacity: false, MotorcycleCapacityUnits: 1, ReservedForUserId: null),
        });

        Assert.True(Assert.Single(slots, s => s.SlotId.Value == "EV-01").HasCharger);
        Assert.True(Assert.Single(slots, s => s.SlotId.Value == "ACC-01").IsAccessible);
    }

    [Fact]
    public void ProjectSlots_ExpandsMotorcycleAreaIntoUnits()
    {
        var slots = ConfigurationSlotService.ProjectSlots(new[]
        {
            new ConfigurationSlot("MOTO-01", IsActive: true, HasCharger: false, IsAccessible: false,
                IsCompanyCarOnly: false, IsMotorcycleCapacity: true, MotorcycleCapacityUnits: 4, ReservedForUserId: null),
        });

        Assert.Equal(4, slots.Count);
        Assert.All(slots, s => Assert.True(s.IsMotorcycleCapacity));
        Assert.Equal(
            new[] { "MOTO-01-1", "MOTO-01-2", "MOTO-01-3", "MOTO-01-4" },
            slots.Select(s => s.SlotId.Value).ToArray());
    }

    [Fact]
    public void ProjectSlots_SingleUnitSlot_KeepsOriginalId()
    {
        var slots = ConfigurationSlotService.ProjectSlots(new[]
        {
            new ConfigurationSlot("MOTO-SOLO", IsActive: true, HasCharger: false, IsAccessible: false,
                IsCompanyCarOnly: false, IsMotorcycleCapacity: true, MotorcycleCapacityUnits: 1, ReservedForUserId: null),
        });

        Assert.Equal("MOTO-SOLO", Assert.Single(slots).SlotId.Value);
    }

    [Fact]
    public void ProjectSlots_Empty_ReturnsEmpty()
    {
        Assert.Empty(ConfigurationSlotService.ProjectSlots(Array.Empty<ConfigurationSlot>()));
    }
}
