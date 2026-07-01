using Dapr.Client;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Moq;

namespace FPS.Configuration.Tests.Infrastructure;

/// <summary>
/// Exercises the destructive tenant purge against a mocked DaprClient backed by a single
/// in-process dictionary shared by all three Configuration repositories. Data is written through
/// the real repositories (so the write-path <c>config-locations</c> index is exercised), then the
/// purge is run over the same backing store.
/// </summary>
public sealed class ConfigurationTenantPurgerTests
{
    private const string ConfigStore = "configstore";

    private readonly Dictionary<string, object?> store = new();
    private readonly DaprClient client;

    public ConfigurationTenantPurgerTests()
    {
        var mock = new Mock<DaprClient>();

        SetupSaveGet<List<ParkingPolicy>>(mock);
        SetupSaveGet<List<ParkingSlot>>(mock);
        SetupSaveGet<List<SlotChangeRecord>>(mock);
        SetupSaveGet<List<string>>(mock);

        mock.Setup(c => c.DeleteStateAsync(
                ConfigStore, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, _, _, _) => store.Remove(key))
            .Returns(Task.CompletedTask);

        client = mock.Object;
    }

    private void SetupSaveGet<T>(Mock<DaprClient> mock) where T : class
    {
        mock.Setup(c => c.SaveStateAsync(
                ConfigStore, It.IsAny<string>(), It.IsAny<T>(),
                null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, T, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<T>(
                ConfigStore, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var val) ? val as T : null);
    }

    private static ParkingPolicy MakePolicy(string tenantId, string? locationId = null) =>
        new()
        {
            TenantId = tenantId,
            LocationId = locationId,
            TimeZone = "Europe/Prague",
            DrawCutOffTime = new TimeOnly(18, 0),
            DailyRequestCap = 100,
            AllocationLookbackDays = 10,
            LateCancellationPenalty = 1,
            NoShowPenalty = 2,
            PublishedByUserId = "test",
            PublishedAt = DateTimeOffset.UtcNow,
        };

    private static List<ParkingSlot> MakeSlots(string tenantId, string locationId, int count) =>
        Enumerable.Range(1, count).Select(i => new ParkingSlot
        {
            SlotId = $"S{i}",
            TenantId = tenantId,
            LocationId = locationId,
            IsActive = true,
        }).ToList();

    private async Task SeedTenantAsync(string tenantId, params string[] locationIds)
    {
        var policyRepo = new DaprParkingPolicyRepository(client);
        var slotRepo = new DaprParkingSlotRepository(client);
        var slotChangeRepo = new DaprSlotChangeRepository(client);

        await policyRepo.SaveAsync(MakePolicy(tenantId));

        foreach (var locationId in locationIds)
        {
            await policyRepo.SaveAsync(MakePolicy(tenantId, locationId));
            await slotRepo.ReplaceLocationSlotsAsync(tenantId, locationId, MakeSlots(tenantId, locationId, 3));
            await slotChangeRepo.RecordAsync(new SlotChangeRecord
            {
                TenantId = tenantId,
                LocationId = locationId,
                ChangedByUserId = "test",
                ChangedAt = DateTimeOffset.UtcNow,
                ChangeReason = "seed",
                SlotCount = 3,
            });
        }
    }

    // ── Write path maintains the location index ───────────────────────────────

    [Fact]
    public async Task Writes_PopulateLocationIndex()
    {
        await SeedTenantAsync("demo", "Prague", "Brno");

        var indexKey = TenantStorageKey.For("config-locations", "demo", "all");
        Assert.True(store.ContainsKey(indexKey));

        var index = Assert.IsType<List<string>>(store[indexKey]);
        // Stored canonically (lower-invariant), de-duplicated across the three write points.
        Assert.Equal(new[] { "prague", "brno" }, index);
    }

    // ── Purge removes every tenant key including the index ────────────────────

    [Fact]
    public async Task Purge_RemovesAllTenantKeysAndIndex()
    {
        await SeedTenantAsync("demo", "Prague", "Brno");
        var purger = new ConfigurationTenantPurger(client);

        var removed = await purger.PurgeTenantAsync("demo");

        // tenant-default (1) + 2 locations × (override + slots + slot-change) = 7 keys.
        Assert.Equal(7, removed);
        Assert.DoesNotContain(store.Keys, k => k.Contains(":demo:", StringComparison.Ordinal));
        Assert.Empty(store);
    }

    // ── Purge is idempotent ───────────────────────────────────────────────────

    [Fact]
    public async Task Purge_SecondRun_ReturnsZero()
    {
        await SeedTenantAsync("demo", "Prague", "Brno");
        var purger = new ConfigurationTenantPurger(client);

        await purger.PurgeTenantAsync("demo");
        var second = await purger.PurgeTenantAsync("demo");

        Assert.Equal(0, second);
    }

    [Fact]
    public async Task Purge_UnknownTenant_ReturnsZero()
    {
        var purger = new ConfigurationTenantPurger(client);
        Assert.Equal(0, await purger.PurgeTenantAsync("never-seeded"));
    }

    // ── Tenant isolation: purging one tenant leaves the other intact ──────────

    [Fact]
    public async Task Purge_DoesNotTouchOtherTenant()
    {
        await SeedTenantAsync("demo", "Prague");
        await SeedTenantAsync("other-co", "Vienna");
        var purger = new ConfigurationTenantPurger(client);

        await purger.PurgeTenantAsync("demo");

        Assert.DoesNotContain(store.Keys, k => k.Contains(":demo:", StringComparison.Ordinal));
        Assert.Contains(store.Keys, k => k.Contains(":other-co:", StringComparison.Ordinal));
    }
}
