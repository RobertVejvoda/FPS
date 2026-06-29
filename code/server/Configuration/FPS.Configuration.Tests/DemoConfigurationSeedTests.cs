using FPS.Configuration.Application;

namespace FPS.Configuration.Tests;

public sealed class DemoConfigurationSeedTests
{
    private const string TenantId = "demo";
    private const string LocationId = "Prague";

    [Fact]
    public void BuildSlots_SeedsTwentyActiveSlotsForTheTenantAndLocation()
    {
        var slots = DemoConfigurationSeed.BuildSlots(TenantId, LocationId);

        Assert.Equal(20, slots.Count);
        Assert.All(slots, s =>
        {
            Assert.True(s.IsActive);
            Assert.Equal(TenantId, s.TenantId);
            Assert.Equal(LocationId, s.LocationId);
        });
    }

    [Fact]
    public void BuildSlots_UsesHumanReadableLabelsNotBareNumbers()
    {
        var slots = DemoConfigurationSeed.BuildSlots(TenantId, LocationId);

        // The SlotId is what the parking map and HR views render, so it must read as a
        // human label (zone-prefixed), never a bare internal number like "301".
        Assert.All(slots, s =>
        {
            Assert.Contains('-', s.SlotId);
            Assert.False(int.TryParse(s.SlotId, out _), $"SlotId '{s.SlotId}' looks like a bare id");
        });
        Assert.Contains(slots, s => s.SlotId == "A-01");
        Assert.Contains(slots, s => s.SlotId == "MOTO-01");
    }

    [Fact]
    public void BuildSlots_CoversEveryAllocationPath()
    {
        var slots = DemoConfigurationSeed.BuildSlots(TenantId, LocationId);

        Assert.Equal(13, slots.Count(s =>
            !s.HasCharger && !s.IsAccessible && !s.IsCompanyCarOnly && !s.IsMotorcycleCapacity));
        Assert.Equal(3, slots.Count(s => s.HasCharger));
        Assert.Equal(1, slots.Count(s => s.IsAccessible));
        Assert.Equal(2, slots.Count(s => s.IsCompanyCarOnly));

        var motorcycle = Assert.Single(slots, s => s.IsMotorcycleCapacity);
        Assert.Equal(4, motorcycle.MotorcycleCapacityUnits);
        Assert.Equal(4, motorcycle.EffectiveMotorcycleCapacityUnits);
    }
}
