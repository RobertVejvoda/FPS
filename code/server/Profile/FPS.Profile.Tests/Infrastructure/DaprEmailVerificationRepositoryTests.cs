using Dapr.Client;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Moq;

namespace FPS.Profile.Tests.Infrastructure;

// AUTH008 (#729) — the Dapr-backed verification repository must use the shared tenant storage-key
// contract (TenantStorageKey.For, sanitised tenant segment) and purge tenant-scoped, keeping other
// tenants intact and staying idempotent.
public sealed class DaprEmailVerificationRepositoryTests
{
    private const string StoreName = "profilestore";
    private readonly Dictionary<string, object?> store = new();

    private DaprEmailVerificationRepository BuildRepo()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(
                StoreName, It.IsAny<string>(), It.IsAny<EmailVerification>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, EmailVerification, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(
                StoreName, It.IsAny<string>(), It.IsAny<List<string>>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<string>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<EmailVerification>(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as EmailVerification : null);

        mock.Setup(c => c.GetStateAsync<List<string>>(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as List<string> : null);

        mock.Setup(c => c.DeleteStateAsync(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, _, _, _) => store.Remove(key))
            .Returns(Task.CompletedTask);

        return new DaprEmailVerificationRepository(mock.Object);
    }

    private static EmailVerification Record(string tenantId, string userId) => new()
    {
        TenantId = tenantId,
        UserId = userId,
        EmailAddress = $"{userId}@{tenantId}.example",
        TokenHash = "hash",
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task SaveAsync_UsesSharedTenantStorageKeyContract()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(Record("demo", "user-1"));

        // Keys must be the sanitised, contract-shaped tenant keys — not ad-hoc interpolation.
        Assert.Contains(TenantStorageKey.For("email-verification", "demo", "user-1"), store.Keys);
        Assert.Contains(TenantStorageKey.For("email-verification-index", "demo", "all"), store.Keys);
    }

    [Fact]
    public async Task GetAsync_AfterSave_RoundTrips_AcrossRepoInstances()
    {
        var repo1 = BuildRepo();
        await repo1.SaveAsync(Record("demo", "user-1"));

        var repo2 = BuildRepo(); // shares the backing store (simulated restart)
        var result = await repo2.GetAsync("demo", "user-1");

        Assert.NotNull(result);
        Assert.Equal("user-1@demo.example", result!.EmailAddress);
    }

    [Fact]
    public async Task PurgeTenantAsync_RemovesOwnTenant_KeepsOthers_AndIsIdempotent()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(Record("demo", "user-1"));
        await repo.SaveAsync(Record("demo", "user-2"));
        await repo.SaveAsync(Record("other", "user-1"));

        var removed = await repo.PurgeTenantAsync("demo");

        Assert.Equal(2, removed);
        Assert.Null(await repo.GetAsync("demo", "user-1"));
        Assert.Null(await repo.GetAsync("demo", "user-2"));
        Assert.NotNull(await repo.GetAsync("other", "user-1"));
        Assert.DoesNotContain(TenantStorageKey.For("email-verification-index", "demo", "all"), store.Keys);

        Assert.Equal(0, await repo.PurgeTenantAsync("demo")); // idempotent
    }
}
