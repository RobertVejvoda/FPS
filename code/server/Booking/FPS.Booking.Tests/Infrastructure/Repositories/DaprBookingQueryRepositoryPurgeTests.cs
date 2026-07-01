using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Infrastructure.Repositories;
using FPS.SharedKernel.Infrastructure;
using Moq;
using Xunit;

namespace FPS.Booking.Infrastructure.Tests.Repositories;

/// <summary>
/// Store-backed tests for the PLAT003C destructive tenant purge. A single Dictionary backs the
/// mocked <see cref="DaprClient"/> so seeding, deletion and idempotency behave like a real store.
/// Generic <c>It.IsAnyType</c> setups are used because the booking indexes are private nested
/// types on the repository and cannot be named in typed setups.
/// </summary>
public sealed class DaprBookingQueryRepositoryPurgeTests
{
    private const string StoreName = "bookingstore";
    private const string Tenant = "tenant-1";

    private readonly Dictionary<string, object?> store = new(StringComparer.Ordinal);
    private readonly Mock<DaprClient> mock = new();
    private readonly DaprBookingQueryRepository repository;

    public DaprBookingQueryRepositoryPurgeTests()
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

        repository = new DaprBookingQueryRepository(mock.Object);
    }

    [Fact]
    public async Task PurgeTenantAsync_RemovesEveryTenantKey_AndReturnsRequestCount()
    {
        await SeedRequestAsync(Guid.NewGuid(), "userA", pending: true, withPenaltyDrawMetrics: true);
        await SeedRequestAsync(Guid.NewGuid(), "userB", pending: false, withPenaltyDrawMetrics: false);
        await SeedRequestAsync(Guid.NewGuid(), "userA", pending: false, withPenaltyDrawMetrics: false);

        // A second tenant must be untouched.
        var otherRequestId = Guid.NewGuid();
        var otherRequestKey = TenantStorageKey.For("request", "othertenant", otherRequestId);
        store[otherRequestKey] = NewDto(otherRequestId, "otheruser");

        var removed = await repository.PurgeTenantAsync(Tenant);

        Assert.Equal(3, removed);
        Assert.DoesNotContain(store.Keys, k => k.Contains(Tenant, StringComparison.Ordinal));
        Assert.Contains(otherRequestKey, store.Keys);
    }

    [Fact]
    public async Task PurgeTenantAsync_SecondCall_ReturnsZero_AndThrowsNothing()
    {
        await SeedRequestAsync(Guid.NewGuid(), "userA", pending: true, withPenaltyDrawMetrics: true);
        await SeedRequestAsync(Guid.NewGuid(), "userB", pending: false, withPenaltyDrawMetrics: false);

        Assert.Equal(2, await repository.PurgeTenantAsync(Tenant));
        Assert.Equal(0, await repository.PurgeTenantAsync(Tenant)); // idempotent
        Assert.Empty(store);
    }

    [Fact]
    public async Task PurgeTenantAsync_EmptyTenant_ReturnsZero()
    {
        Assert.Equal(0, await repository.PurgeTenantAsync(Tenant));
    }

    private async Task SeedRequestAsync(Guid requestId, string requestor, bool pending, bool withPenaltyDrawMetrics)
    {
        var dto = NewDto(requestId, requestor);
        store[TenantStorageKey.For("request", Tenant, requestId)] = dto;

        await repository.AddToTenantOpsIndexAsync(Tenant, requestId);
        await repository.AddToUserIndexAsync(Tenant, requestor, requestId);
        if (pending)
            await repository.AddToTenantPendingIndexAsync(Tenant, requestId);

        if (!withPenaltyDrawMetrics)
            return;

        store[TenantStorageKey.For("penalty", Tenant, $"{requestId}:NoShow")] = new PenaltyDto
        {
            Id = Guid.NewGuid(), RequestId = requestId, TenantId = Tenant, RequestorId = requestor, Type = "NoShow"
        };

        // Draw attempts are no longer purged by the query repository — DaprDrawRepository owns them
        // via its own per-tenant draw index (see DaprDrawRepositoryPurgeTests).

        store[TenantStorageKey.For("metrics", Tenant, requestor)] = new List<string> { "2026-06-02" };
        store[$"count:{Tenant}:2026-06-02"] = 1;
    }

    private static BookingRequestDto NewDto(Guid requestId, string requestor) => new()
    {
        RequestId = requestId,
        TenantId = Tenant,
        RequestedBy = requestor,
        LocationId = "loc-1",
        PlannedArrivalTime = new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc),
        PlannedDepartureTime = new DateTime(2026, 6, 2, 11, 0, 0, DateTimeKind.Utc),
        Status = "Pending"
    };
}
