using Dapr.Client;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

public sealed class DaprHrRosterStoreTests
{
    private const string StoreName = "notificationstore";
    private readonly Dictionary<string, object?> store = new();

    private DaprHrRosterStore BuildStore()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<List<string>>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<string>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = new List<string>(value))
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<List<string>>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as List<string> : null);

        return new DaprHrRosterStore(mock.Object, NullLogger<DaprHrRosterStore>.Instance);
    }

    // ── Restart persistence ───────────────────────────────────────────────────

    [Fact]
    public async Task Set_ThenRestart_HydrateRestoresRoster()
    {
        var store1 = BuildStore();
        store1.Set("demo", ["hr-admin", "hr-user"]);

        var store2 = BuildStore();
        await store2.HydrateAsync();

        var users = store2.GetHrUserIds("demo");
        Assert.Equal(2, users.Count);
        Assert.Contains("hr-admin", users);
        Assert.Contains("hr-user", users);
    }

    [Fact]
    public async Task HydrateAsync_Empty_WhenNoDataPersisted()
    {
        var rosterStore = BuildStore();
        await rosterStore.HydrateAsync();

        Assert.Empty(rosterStore.GetHrUserIds("unknown-tenant"));
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task Set_MultiTenant_HydratesAllTenants()
    {
        var store1 = BuildStore();
        store1.Set("tenant-a", ["hr-1"]);
        store1.Set("tenant-b", ["hr-2", "hr-3"]);

        var store2 = BuildStore();
        await store2.HydrateAsync();

        Assert.Single(store2.GetHrUserIds("tenant-a"));
        Assert.Equal(2, store2.GetHrUserIds("tenant-b").Count);
    }

    [Fact]
    public void GetHrUserIds_ReturnsEmpty_ForUnknownTenant()
    {
        var rosterStore = BuildStore();
        rosterStore.Set("tenant-a", ["hr-1"]);

        Assert.Empty(rosterStore.GetHrUserIds("tenant-b"));
    }

    // ── Fan-out / roster update ───────────────────────────────────────────────

    [Fact]
    public async Task Set_OverwritesExistingRoster_AndPersistsNewList()
    {
        var store1 = BuildStore();
        store1.Set("demo", ["hr-old"]);
        store1.Set("demo", ["hr-new-1", "hr-new-2"]);

        var store2 = BuildStore();
        await store2.HydrateAsync();

        var users = store2.GetHrUserIds("demo");
        Assert.Equal(2, users.Count);
        Assert.DoesNotContain("hr-old", users);
    }

    [Fact]
    public void Set_FiltersEmptyUserIds()
    {
        var rosterStore = BuildStore();
        rosterStore.Set("demo", ["hr-1", "", "hr-2"]);

        var users = rosterStore.GetHrUserIds("demo");
        Assert.Equal(2, users.Count);
        Assert.DoesNotContain(string.Empty, users);
    }

    // ── Persistence failure path ──────────────────────────────────────────────

    [Fact]
    public void Set_WhenDaprUnavailable_SetsRosterInMemoryAndMarksDegraded()
    {
        var mock = new Mock<DaprClient>();
        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<List<string>>(), null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dapr sidecar unavailable"));

        var rosterStore = new DaprHrRosterStore(mock.Object, NullLogger<DaprHrRosterStore>.Instance);
        rosterStore.Set("demo", ["hr-1"]);

        // In-memory cache populated (best-effort), health gate flags the failure.
        Assert.Contains("hr-1", rosterStore.GetHrUserIds("demo"));
        Assert.True(rosterStore.IsRosterPersistenceDegraded);
    }

    [Fact]
    public void Set_AfterSuccessfulWrite_ClearsDegradedFlag()
    {
        var failMock = new Mock<DaprClient>();
        failMock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<List<string>>(), null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dapr sidecar unavailable"));
        var failStore = new DaprHrRosterStore(failMock.Object, NullLogger<DaprHrRosterStore>.Instance);
        failStore.Set("demo", ["hr-1"]);
        Assert.True(failStore.IsRosterPersistenceDegraded);

        // Successful write clears the flag.
        var successStore = BuildStore();
        successStore.Set("demo", ["hr-1"]);
        Assert.False(successStore.IsRosterPersistenceDegraded);
    }
}
