using Dapr.Client;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;
using Moq;

namespace FPS.Configuration.Tests.Infrastructure;

public sealed class DaprParkingSlotRepositoryTests
{
    private const string ConfigStore = "configstore";
    private readonly Dictionary<string, object?> store = new();

    private DaprParkingSlotRepository BuildRepo()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(
                ConfigStore, It.IsAny<string>(), It.IsAny<List<ParkingSlot>>(),
                null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<ParkingSlot>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<List<ParkingSlot>>(
                ConfigStore, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var val) ? val as List<ParkingSlot> : null);

        return new DaprParkingSlotRepository(mock.Object);
    }

    private static List<ParkingSlot> MakeSlots(string tenantId, string locationId, int count) =>
        Enumerable.Range(1, count).Select(i => new ParkingSlot
        {
            SlotId = $"S{i}",
            TenantId = tenantId,
            LocationId = locationId,
            IsActive = true,
        }).ToList();

    // ── Cold-restart persistence ──────────────────────────────────────────────

    [Fact]
    public async Task GetByLocation_AfterReplace_ReturnsSavedSlots()
    {
        var slots = MakeSlots("demo", "Prague", 5);
        var repo1 = BuildRepo();
        await repo1.ReplaceLocationSlotsAsync("demo", "Prague", slots);

        var repo2 = BuildRepo(); // simulates restart
        var result = await repo2.GetByLocationAsync("demo", "Prague");

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetByLocation_EmptyWhenNoneStored()
    {
        var result = await BuildRepo().GetByLocationAsync("demo", "Prague");
        Assert.Empty(result);
    }

    // ── ReplaceLocationSlotsAsync replaces entire list ────────────────────────

    [Fact]
    public async Task Replace_OverwritesPreviousSlots()
    {
        var repo = BuildRepo();
        await repo.ReplaceLocationSlotsAsync("demo", "Prague", MakeSlots("demo", "Prague", 10));
        await repo.ReplaceLocationSlotsAsync("demo", "Prague", MakeSlots("demo", "Prague", 3));

        var result = await repo.GetByLocationAsync("demo", "Prague");
        Assert.Equal(3, result.Count);
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task Slots_IsolatedByTenant()
    {
        var repo = BuildRepo();
        await repo.ReplaceLocationSlotsAsync("demo", "Prague", MakeSlots("demo", "Prague", 5));

        var result = await repo.GetByLocationAsync("other-co", "Prague");
        Assert.Empty(result);
    }

    // ── Location isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task Slots_IsolatedByLocation()
    {
        var repo = BuildRepo();
        await repo.ReplaceLocationSlotsAsync("demo", "Prague", MakeSlots("demo", "Prague", 5));

        var result = await repo.GetByLocationAsync("demo", "Brno");
        Assert.Empty(result);
    }
}
