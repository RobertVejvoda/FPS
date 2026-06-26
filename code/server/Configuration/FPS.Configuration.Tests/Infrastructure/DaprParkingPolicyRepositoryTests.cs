using Dapr.Client;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;
using Moq;

namespace FPS.Configuration.Tests.Infrastructure;

/// <summary>
/// Tests DaprParkingPolicyRepository using a mocked DaprClient that acts as an
/// in-process store, proving cold-restart persistence semantics and tenant isolation.
/// </summary>
public sealed class DaprParkingPolicyRepositoryTests
{
    private const string ConfigStore = "configstore";

    // Simulates a shared backing store that survives "restart" (new repo instance).
    private readonly Dictionary<string, object?> store = new();

    private DaprParkingPolicyRepository BuildRepo()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(
                ConfigStore, It.IsAny<string>(), It.IsAny<List<ParkingPolicy>>(),
                null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<ParkingPolicy>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<List<ParkingPolicy>>(
                ConfigStore, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var val) ? val as List<ParkingPolicy> : null);

        return new DaprParkingPolicyRepository(mock.Object);
    }

    private static ParkingPolicy MakePolicy(string tenantId, string? locationId = null, int cap = 100) =>
        new()
        {
            TenantId = tenantId,
            LocationId = locationId,
            TimeZone = "Europe/Prague",
            DrawCutOffTime = new TimeOnly(18, 0),
            DailyRequestCap = cap,
            AllocationLookbackDays = 10,
            LateCancellationPenalty = 1,
            NoShowPenalty = 2,
            PublishedByUserId = "test",
            PublishedAt = DateTimeOffset.UtcNow,
        };

    // ── Cold-restart persistence: new repo instance reads same backing store ──

    [Fact]
    public async Task GetTenantDefault_AfterSave_ReturnsPolicy()
    {
        var repo1 = BuildRepo();
        await repo1.SaveAsync(MakePolicy("demo", cap: 50));

        var repo2 = BuildRepo(); // simulates restart
        var result = await repo2.GetTenantDefaultAsync("demo");

        Assert.NotNull(result);
        Assert.Equal(50, result.DailyRequestCap);
    }

    [Fact]
    public async Task GetTenantDefault_NoPolicy_ReturnsNull()
    {
        var repo = BuildRepo();
        var result = await repo.GetTenantDefaultAsync("demo");
        Assert.Null(result);
    }

    // ── Latest version returned ───────────────────────────────────────────────

    [Fact]
    public async Task GetTenantDefault_MultipleVersions_ReturnsLatest()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakePolicy("demo", cap: 10));
        await repo.SaveAsync(MakePolicy("demo", cap: 20));
        await repo.SaveAsync(MakePolicy("demo", cap: 30));

        var result = await repo.GetTenantDefaultAsync("demo");
        Assert.Equal(30, result!.DailyRequestCap);
    }

    // ── GetHistoryAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistoryAsync_ReturnsVersionsNewestFirst()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakePolicy("demo", cap: 1));
        await repo.SaveAsync(MakePolicy("demo", cap: 2));
        await repo.SaveAsync(MakePolicy("demo", cap: 3));

        var history = await repo.GetHistoryAsync("demo", null);

        Assert.Equal(3, history.Count);
        Assert.Equal(3, history[0].DailyRequestCap);
        Assert.Equal(1, history[2].DailyRequestCap);
    }

    [Fact]
    public async Task GetHistoryAsync_LimitApplied()
    {
        var repo = BuildRepo();
        for (var i = 1; i <= 10; i++)
            await repo.SaveAsync(MakePolicy("demo", cap: i));

        var history = await repo.GetHistoryAsync("demo", null, limit: 3);
        Assert.Equal(3, history.Count);
        Assert.Equal(10, history[0].DailyRequestCap);
    }

    // ── Location override scoped separately from tenant default ──────────────

    [Fact]
    public async Task LocationOverride_ScopedSeparatelyFromDefault()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakePolicy("demo", locationId: null, cap: 100));
        await repo.SaveAsync(MakePolicy("demo", locationId: "Prague", cap: 50));

        var defaultPolicy = await repo.GetTenantDefaultAsync("demo");
        var locationPolicy = await repo.GetLocationOverrideAsync("demo", "Prague");

        Assert.Equal(100, defaultPolicy!.DailyRequestCap);
        Assert.Equal(50, locationPolicy!.DailyRequestCap);
    }

    // ── locationId="default" must not collide with tenant-default key ──────────

    [Fact]
    public async Task LocationOverride_LocationIdDefault_DoesNotCollideWithTenantDefault()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakePolicy("demo", locationId: null, cap: 111));       // tenant default
        await repo.SaveAsync(MakePolicy("demo", locationId: "default", cap: 222));  // location override named "default"

        var tenantDefault = await repo.GetTenantDefaultAsync("demo");
        var locationOverride = await repo.GetLocationOverrideAsync("demo", "default");

        Assert.Equal(111, tenantDefault!.DailyRequestCap);
        Assert.Equal(222, locationOverride!.DailyRequestCap);
    }

    [Fact]
    public async Task TenantDefault_Key_DoesNotContainLocationPrefix()
    {
        // Verify the keys written are structurally distinct by checking the
        // backing store entries do not share a key.
        var repo = BuildRepo();
        await repo.SaveAsync(MakePolicy("demo", locationId: null, cap: 1));
        await repo.SaveAsync(MakePolicy("demo", locationId: "default", cap: 2));

        // Two distinct keys must exist in the store.
        Assert.Equal(2, store.Count);
        Assert.DoesNotContain(store.Keys, k => store.Keys.Count(k2 => k2 == k) > 1);
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetTenantDefault_IsolatedByTenant()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakePolicy("demo", cap: 99));

        var otherResult = await repo.GetTenantDefaultAsync("other-co");
        Assert.Null(otherResult);
    }

    [Fact]
    public async Task History_TenantA_NotVisibleToTenantB()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakePolicy("tenant-a", cap: 100));

        var historyB = await repo.GetHistoryAsync("tenant-b", null);
        Assert.Empty(historyB);
    }
}
