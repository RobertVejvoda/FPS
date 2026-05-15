using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;

namespace FPS.Configuration.Tests;

public sealed class ParkingSlotServiceTests
{
    private static ParkingSlot Slot(string slotId, string tenantId = "tenant-1", string locationId = "loc-1") =>
        new() { SlotId = slotId, TenantId = tenantId, LocationId = locationId, IsActive = true };

    [Fact]
    public async Task ReplaceSlots_ValidList_PersistsAndReturnsNoErrors()
    {
        var repo = new InMemoryParkingSlotRepository();
        var service = new ParkingSlotService(repo);

        var slots = new List<ParkingSlot> { Slot("S1"), Slot("S2") };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", slots, default);

        Assert.Empty(errors);
        var result = await service.GetByLocationAsync("tenant-1", "loc-1", default);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ReplaceSlots_EmptySlotId_ReturnsError()
    {
        var repo = new InMemoryParkingSlotRepository();
        var service = new ParkingSlotService(repo);

        var slots = new List<ParkingSlot> { Slot("") };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", slots, default);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("slotId"));
    }

    [Fact]
    public async Task ReplaceSlots_DuplicateSlotId_ReturnsError()
    {
        var repo = new InMemoryParkingSlotRepository();
        var service = new ParkingSlotService(repo);

        var slots = new List<ParkingSlot> { Slot("S1"), Slot("S1") };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", slots, default);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("S1"));
    }

    [Fact]
    public async Task ReplaceSlots_IsIdempotent()
    {
        var repo = new InMemoryParkingSlotRepository();
        var service = new ParkingSlotService(repo);

        var firstBatch = new List<ParkingSlot> { Slot("S1"), Slot("S2") };
        await service.ReplaceAsync("tenant-1", "loc-1", firstBatch, default);

        var secondBatch = new List<ParkingSlot> { Slot("S3") };
        await service.ReplaceAsync("tenant-1", "loc-1", secondBatch, default);

        var result = await service.GetByLocationAsync("tenant-1", "loc-1", default);
        Assert.Single(result);
        Assert.Equal("S3", result[0].SlotId);
    }

    [Fact]
    public async Task GetByLocation_EmptyWhenNoSlots()
    {
        var repo = new InMemoryParkingSlotRepository();
        var service = new ParkingSlotService(repo);

        var result = await service.GetByLocationAsync("tenant-1", "loc-X", default);
        Assert.Empty(result);
    }

    [Fact]
    public async Task TenantIsolation_TenantACannotSeeTenantBSlots()
    {
        var repo = new InMemoryParkingSlotRepository();
        var service = new ParkingSlotService(repo);

        await service.ReplaceAsync("tenant-A", "loc-1", [Slot("S1", "tenant-A")], default);

        var result = await service.GetByLocationAsync("tenant-B", "loc-1", default);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SlotCapabilities_StoredAndRetrievedCorrectly()
    {
        var repo = new InMemoryParkingSlotRepository();
        var service = new ParkingSlotService(repo);

        var slot = new ParkingSlot
        {
            SlotId = "EV-01",
            TenantId = "tenant-1",
            LocationId = "loc-1",
            IsActive = true,
            HasCharger = true,
            IsAccessible = false,
            IsCompanyCarOnly = true,
            IsMotorcycleCapacity = false,
            ReservedForUserId = "user-vip"
        };

        await service.ReplaceAsync("tenant-1", "loc-1", [slot], default);
        var result = await service.GetByLocationAsync("tenant-1", "loc-1", default);

        var retrieved = result.Single();
        Assert.True(retrieved.HasCharger);
        Assert.True(retrieved.IsCompanyCarOnly);
        Assert.Equal("user-vip", retrieved.ReservedForUserId);
    }
}
