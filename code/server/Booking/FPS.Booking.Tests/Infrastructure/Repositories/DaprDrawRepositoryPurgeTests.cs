using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Infrastructure.Repositories;
using FPS.SharedKernel.Infrastructure;
using Moq;
using Xunit;

namespace FPS.Booking.Infrastructure.Tests.Repositories;

/// <summary>
/// Store-backed tests for the PLAT003C draw purge. A single Dictionary backs the mocked
/// <see cref="DaprClient"/> so saving (which appends to the per-tenant draw index), deletion
/// and idempotency behave like a real store. Crucially this covers the orphan case Codex
/// raised: a draw attempt persisted directly with no matching booking-request row is still
/// reached via the index.
/// </summary>
public sealed class DaprDrawRepositoryPurgeTests
{
    private const string StoreName = "bookingstore";
    private const string Tenant = "tenant-1";
    private const string DrawIndexKey = "draw-index:tenant-1:all";

    private readonly Dictionary<string, object?> store = new(StringComparer.Ordinal);
    private readonly Mock<DaprClient> mock = new();
    private readonly DaprDrawRepository repository;

    public DaprDrawRepositoryPurgeTests()
    {
        mock.Setup(c => c.GetStateAsync<It.IsAnyType>(
                StoreName, It.IsAny<string>(), It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var key = (string)invocation.Arguments[1];
                var valueType = invocation.Method.GetGenericArguments()[0];
                store.TryGetValue(key, out var value);
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(valueType)
                    .Invoke(null, new[] { value })!;
            }));

        mock.Setup(c => c.SaveStateAsync<It.IsAnyType>(
                StoreName, It.IsAny<string>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<StateOptions?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(invocation =>
                store[(string)invocation.Arguments[1]] = invocation.Arguments[2]))
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.DeleteStateAsync(
                StoreName, It.IsAny<string>(), It.IsAny<StateOptions?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, _, _, _) => store.Remove(key))
            .Returns(Task.CompletedTask);

        repository = new DaprDrawRepository(mock.Object);
    }

    [Fact]
    public async Task SaveAsync_AppendsDrawKeyToTenantIndex()
    {
        var attempt = NewAttempt("draw:tenant-1:loc-1:2026-06-02:0900");

        await repository.SaveAsync(attempt);

        var index = Assert.IsType<List<string>>(store[DrawIndexKey]);
        Assert.Equal(new[] { attempt.DrawKey }, index);
    }

    [Fact]
    public async Task SaveAsync_SameKeyTwice_IndexStaysDeduplicated()
    {
        var attempt = NewAttempt("draw:tenant-1:loc-1:2026-06-02:0900");

        await repository.SaveAsync(attempt);
        await repository.SaveAsync(attempt);

        var index = Assert.IsType<List<string>>(store[DrawIndexKey]);
        Assert.Single(index);
    }

    [Fact]
    public async Task TrySaveAsync_AppendsDrawKeyToTenantIndex()
    {
        var attempt = NewAttempt("draw:tenant-1:loc-1:2026-06-02:0900");

        var saved = await repository.TrySaveAsync(attempt);

        Assert.True(saved);
        var index = Assert.IsType<List<string>>(store[DrawIndexKey]);
        Assert.Contains(attempt.DrawKey, index);
    }

    [Fact]
    public async Task PurgeTenantAsync_DeletesEveryAttempt_IncludingOrphan_AndTheIndex()
    {
        var withRequest = NewAttempt("draw:tenant-1:loc-1:2026-06-02:0900");
        var alsoWithRequest = NewAttempt("draw:tenant-1:loc-1:2026-06-03:0900");
        // Orphan: an empty/failed run persisted directly by SaveAsync with no booking-request row —
        // the reconstruct-from-requests approach would have missed this key entirely.
        var orphan = NewAttempt("draw:tenant-1:loc-9:2026-06-30:1700");

        await repository.SaveAsync(withRequest);
        await repository.SaveAsync(alsoWithRequest);
        await repository.SaveAsync(orphan);

        // A second tenant's draw + index must survive.
        var otherKey = "draw:othertenant:loc-1:2026-06-02:0900";
        store[otherKey] = new DrawAttemptDto { DrawKey = otherKey, TenantId = "othertenant" };
        store["draw-index:othertenant:all"] = new List<string> { otherKey };

        var removed = await repository.PurgeTenantAsync(Tenant, CancellationToken.None);

        Assert.Equal(3, removed);
        Assert.DoesNotContain(store.Keys, k => k.Contains(Tenant, StringComparison.Ordinal));
        Assert.Contains(otherKey, store.Keys);
        Assert.Contains("draw-index:othertenant:all", store.Keys);
    }

    [Fact]
    public async Task PurgeTenantAsync_SecondCall_ReturnsZero_AndThrowsNothing()
    {
        await repository.SaveAsync(NewAttempt("draw:tenant-1:loc-1:2026-06-02:0900"));
        await repository.SaveAsync(NewAttempt("draw:tenant-1:loc-1:2026-06-03:0900"));

        Assert.Equal(2, await repository.PurgeTenantAsync(Tenant, CancellationToken.None));
        Assert.Equal(0, await repository.PurgeTenantAsync(Tenant, CancellationToken.None)); // idempotent
        Assert.Empty(store);
    }

    [Fact]
    public async Task PurgeTenantAsync_EmptyTenant_ReturnsZero()
    {
        Assert.Equal(0, await repository.PurgeTenantAsync(Tenant, CancellationToken.None));
    }

    private static DrawAttemptDto NewAttempt(string drawKey) => new()
    {
        DrawKey = drawKey,
        TenantId = Tenant,
        LocationId = "loc-1",
        Date = new DateOnly(2026, 6, 2),
        Status = "Completed"
    };
}
