using Dapr.Client;
using FPS.SharedKernel.Infrastructure;
using Moq;

namespace FPS.Profile.Tests.Infrastructure;

/// <summary>
/// Tests DaprDeactivatedUserStore using a mocked DaprClient backed by a shared
/// in-process dictionary, proving deactivation state survives restart and is
/// correctly scoped per tenant.
/// </summary>
public sealed class DaprDeactivatedUserStoreTests
{
    private const string StoreName = "configstore";

    private readonly Dictionary<string, object?> store = new();

    private DaprDeactivatedUserStore BuildStore()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(
                StoreName, It.IsAny<string>(), It.IsAny<bool>(),
                null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<bool>(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var val) && val is bool b && b);

        return new DaprDeactivatedUserStore(mock.Object);
    }

    // ── Deactivation survives restart (cold-restart persistence) ─────────────

    [Fact]
    public void Deactivate_ThenRestart_UserRemainsDeactivated()
    {
        var store1 = BuildStore();
        store1.Deactivate("demo", "user-1");

        var store2 = BuildStore(); // simulates restart — empty cache, shared Dapr backing
        Assert.True(store2.IsDeactivated("demo", "user-1"));
    }

    [Fact]
    public void Reactivate_ThenRestart_UserIsNoLongerDeactivated()
    {
        var store1 = BuildStore();
        store1.Deactivate("demo", "user-1");
        store1.Reactivate("demo", "user-1");

        var store2 = BuildStore();
        Assert.False(store2.IsDeactivated("demo", "user-1"));
    }

    // ── Basic deactivate / reactivate cycle ────────────────────────────────────

    [Fact]
    public void IsDeactivated_FreshUser_ReturnsFalse()
    {
        var s = BuildStore();
        Assert.False(s.IsDeactivated("demo", "user-1"));
    }

    [Fact]
    public void Deactivate_SetsDeactivated()
    {
        var s = BuildStore();
        s.Deactivate("demo", "user-1");
        Assert.True(s.IsDeactivated("demo", "user-1"));
    }

    [Fact]
    public void Reactivate_ClearsDeactivated()
    {
        var s = BuildStore();
        s.Deactivate("demo", "user-1");
        s.Reactivate("demo", "user-1");
        Assert.False(s.IsDeactivated("demo", "user-1"));
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_TenantA_DoesNotAffectTenantB()
    {
        var s = BuildStore();
        s.Deactivate("demo", "user-1");
        Assert.False(s.IsDeactivated("other-co", "user-1"));
    }

    [Fact]
    public void Deactivate_SameUserIdDifferentTenants_IndependentState()
    {
        var s = BuildStore();
        s.Deactivate("demo", "shared-user");
        s.Reactivate("other-co", "shared-user");

        Assert.True(s.IsDeactivated("demo", "shared-user"));
        Assert.False(s.IsDeactivated("other-co", "shared-user"));
    }

    // ── Write-through cache: second IsDeactivated call uses cache, not Dapr ──

    [Fact]
    public void IsDeactivated_SecondCall_HitsCache()
    {
        var mock = new Mock<DaprClient>();
        mock.Setup(c => c.GetStateAsync<bool>(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mock.Setup(c => c.SaveStateAsync(
                StoreName, It.IsAny<string>(), It.IsAny<bool>(),
                null, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var s = new DaprDeactivatedUserStore(mock.Object);

        _ = s.IsDeactivated("demo", "user-1"); // cache miss → Dapr
        _ = s.IsDeactivated("demo", "user-1"); // cache hit

        // GetStateAsync should only be called once (cache miss path).
        mock.Verify(c => c.GetStateAsync<bool>(
            StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
