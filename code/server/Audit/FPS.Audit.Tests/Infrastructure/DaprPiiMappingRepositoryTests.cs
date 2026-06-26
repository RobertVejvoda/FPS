using Dapr.Client;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using Moq;

namespace FPS.Audit.Tests.Infrastructure;

public sealed class DaprPiiMappingRepositoryTests
{
    private const string StoreName = "pii-mappingstore";
    private readonly Dictionary<string, object?> store = new();

    private DaprPiiMappingRepository BuildRepo()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<PiiMapping>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, PiiMapping, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, string, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<PiiMapping>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as PiiMapping : null);

        mock.Setup(c => c.GetStateAsync<string>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as string : null);

        mock.Setup(c => c.DeleteStateAsync(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, _, _, _) => store.Remove(key))
            .Returns(Task.CompletedTask);

        return new DaprPiiMappingRepository(mock.Object);
    }

    private static PiiMapping MakeMapping(string tenant = "demo", string userId = "user-1", string hash = "hash-abc") =>
        new() { TenantId = tenant, UserId = userId, ActorHash = hash, Name = "Test User", Email = "test@example.com" };

    // ── Save and exist ────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenExistsAsync_ReturnsTrue()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeMapping());
        Assert.True(await repo.ExistsAsync("user-1", "demo"));
    }

    [Fact]
    public async Task ExistsAsync_BeforeSave_ReturnsFalse()
    {
        var repo = BuildRepo();
        Assert.False(await repo.ExistsAsync("user-1", "demo"));
    }

    // ── Cold-restart ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenRestart_MappingSurvives()
    {
        var repo1 = BuildRepo();
        await repo1.SaveAsync(MakeMapping());

        var repo2 = BuildRepo();
        var result = await repo2.GetByActorHashesAsync("demo", ["hash-abc"]);
        Assert.True(result.ContainsKey("hash-abc"));
    }

    // ── Delete by userId ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteByUserIdAsync_RemovesMapping()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeMapping());
        await repo.DeleteByUserIdAsync("user-1", "demo");
        Assert.False(await repo.ExistsAsync("user-1", "demo"));
    }

    [Fact]
    public async Task DeleteByUserIdAsync_RemovesHashIndex()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeMapping());
        await repo.DeleteByUserIdAsync("user-1", "demo");

        var result = await repo.GetByActorHashesAsync("demo", ["hash-abc"]);
        Assert.Empty(result);
    }

    // ── Delete by actorHash ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteByActorHashAsync_RemovesMapping()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeMapping());
        await repo.DeleteByActorHashAsync("hash-abc", "demo");
        Assert.False(await repo.ExistsAsync("user-1", "demo"));
    }

    // ── GetByActorHashesAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetByActorHashesAsync_ReturnsMatchingMappings()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeMapping("demo", "user-1", "hash-abc"));
        await repo.SaveAsync(MakeMapping("demo", "user-2", "hash-def"));

        var result = await repo.GetByActorHashesAsync("demo", ["hash-abc", "hash-def", "hash-xyz"]);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("hash-abc"));
        Assert.True(result.ContainsKey("hash-def"));
        Assert.False(result.ContainsKey("hash-xyz"));
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_TenantIsolation_ReturnsFalseForOtherTenant()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeMapping("demo", "user-1", "hash-abc"));
        Assert.False(await repo.ExistsAsync("user-1", "other-co"));
    }
}
