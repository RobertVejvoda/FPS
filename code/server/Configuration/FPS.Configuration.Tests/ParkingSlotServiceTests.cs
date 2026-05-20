using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;


namespace FPS.Configuration.Tests;

public sealed class ParkingSlotServiceTests
{
    private static ParkingSlot Slot(string slotId, string tenantId = "tenant-1", string locationId = "loc-1") =>
        new() { SlotId = slotId, TenantId = tenantId, LocationId = locationId, IsActive = true };

    private static ParkingSlotService MakeService(out InMemorySlotChangeRepository changeRepo)
    {
        changeRepo = new InMemorySlotChangeRepository();
        return new ParkingSlotService(new InMemoryParkingSlotRepository(), changeRepo);
    }

    [Fact]
    public async Task ReplaceSlots_ValidList_PersistsAndReturnsNoErrors()
    {
        var service = MakeService(out _);
        var slots = new List<ParkingSlot> { Slot("S1"), Slot("S2") };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", slots, "actor-1", null, default);
        Assert.Empty(errors);
        var result = await service.GetByLocationAsync("tenant-1", "loc-1", default);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ReplaceSlots_EmptySlotId_ReturnsError()
    {
        var service = MakeService(out _);
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", [Slot("")], "actor-1", null, default);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("slotId"));
    }

    [Fact]
    public async Task ReplaceSlots_DuplicateSlotId_ReturnsError()
    {
        var service = MakeService(out _);
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", [Slot("S1"), Slot("S1")], "actor-1", null, default);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("S1"));
    }

    [Fact]
    public async Task ReplaceSlots_IsIdempotent()
    {
        var service = MakeService(out _);
        await service.ReplaceAsync("tenant-1", "loc-1", [Slot("S1"), Slot("S2")], "actor-1", null, default);
        await service.ReplaceAsync("tenant-1", "loc-1", [Slot("S3")], "actor-1", null, default);
        var result = await service.GetByLocationAsync("tenant-1", "loc-1", default);
        Assert.Single(result);
        Assert.Equal("S3", result[0].SlotId);
    }

    [Fact]
    public async Task GetByLocation_EmptyWhenNoSlots()
    {
        var service = MakeService(out _);
        Assert.Empty(await service.GetByLocationAsync("tenant-1", "loc-X", default));
    }

    [Fact]
    public async Task TenantIsolation_TenantACannotSeeTenantBSlots()
    {
        var service = MakeService(out _);
        await service.ReplaceAsync("tenant-A", "loc-1", [Slot("S1", "tenant-A")], "actor-1", null, default);
        Assert.Empty(await service.GetByLocationAsync("tenant-B", "loc-1", default));
    }

    [Fact]
    public async Task SlotCapabilities_StoredAndRetrievedCorrectly()
    {
        var service = MakeService(out _);
        var slot = new ParkingSlot
        {
            SlotId = "EV-01", TenantId = "tenant-1", LocationId = "loc-1",
            IsActive = true, HasCharger = true, IsAccessible = false,
            IsCompanyCarOnly = true, IsMotorcycleCapacity = false, ReservedForUserId = "user-vip"
        };
        await service.ReplaceAsync("tenant-1", "loc-1", [slot], "actor-1", null, default);
        var retrieved = (await service.GetByLocationAsync("tenant-1", "loc-1", default)).Single();
        Assert.True(retrieved.HasCharger);
        Assert.True(retrieved.IsCompanyCarOnly);
        Assert.Equal("user-vip", retrieved.ReservedForUserId);
    }

    // ── CFG003: slot change history ─────────────────────────────────────────

    [Fact]
    public async Task ReplaceSlots_RecordsChangeHistory()
    {
        var service = MakeService(out var changeRepo);
        await service.ReplaceAsync("tenant-1", "loc-1", [Slot("S1"), Slot("S2")], "admin-1", "Initial setup", default);

        var history = await changeRepo.GetHistoryAsync("tenant-1", "loc-1");
        Assert.Single(history);
        Assert.Equal("admin-1", history[0].ChangedByUserId);
        Assert.Equal("Initial setup", history[0].ChangeReason);
        Assert.Equal(2, history[0].SlotCount);
    }

    [Fact]
    public async Task ReplaceSlots_ValidationFailure_DoesNotRecordHistory()
    {
        var service = MakeService(out var changeRepo);
        await service.ReplaceAsync("tenant-1", "loc-1", [Slot("")], "actor-1", null, default);

        var history = await changeRepo.GetHistoryAsync("tenant-1", "loc-1");
        Assert.Empty(history);
    }

    [Fact]
    public async Task SlotHistory_IsTenantScoped()
    {
        var service = MakeService(out var changeRepo);
        await service.ReplaceAsync("tenant-A", "loc-1", [Slot("S1", "tenant-A")], "actor-A", null, default);

        var historyB = await changeRepo.GetHistoryAsync("tenant-B", "loc-1");
        Assert.Empty(historyB);
    }

    [Fact]
    public async Task SlotHistory_AccumulatesAcrossChanges()
    {
        var service = MakeService(out var changeRepo);
        await service.ReplaceAsync("tenant-1", "loc-1", [Slot("S1")], "actor-1", "First", default);
        await service.ReplaceAsync("tenant-1", "loc-1", [Slot("S2"), Slot("S3")], "actor-2", "Second", default);

        var history = await changeRepo.GetHistoryAsync("tenant-1", "loc-1");
        Assert.Equal(2, history.Count);
        Assert.Equal("Second", history[0].ChangeReason);  // newest first
        Assert.Equal("First", history[1].ChangeReason);
    }
}
