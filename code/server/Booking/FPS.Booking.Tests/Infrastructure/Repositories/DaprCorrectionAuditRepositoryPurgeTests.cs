using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Infrastructure.Repositories;
using Moq;
using Xunit;

namespace FPS.Booking.Infrastructure.Tests.Repositories;

/// <summary>
/// Store-backed tests for the PLAT003C correction-audit purge. A single Dictionary backs the
/// mocked <see cref="DaprClient"/> so saving (which appends to the per-tenant correction index),
/// deletion and idempotency behave like a real store. Manual-correction audits had no index at
/// all before this slice, so a repeated sandbox reset would have left them behind.
/// </summary>
public sealed class DaprCorrectionAuditRepositoryPurgeTests
{
    private const string StoreName = "bookingstore";
    private const string Tenant = "tenant-1";
    private const string CorrectionIndexKey = "correction-index:tenant-1:all";

    private readonly Dictionary<string, object?> store = new(StringComparer.Ordinal);
    private readonly Mock<DaprClient> mock = new();
    private readonly DaprCorrectionAuditRepository repository;

    public DaprCorrectionAuditRepositoryPurgeTests()
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

        repository = new DaprCorrectionAuditRepository(mock.Object);
    }

    [Fact]
    public async Task SaveAsync_AppendsCorrectionKeyToTenantIndex()
    {
        var audit = NewAudit();

        await repository.SaveAsync(audit);

        var index = Assert.IsType<List<string>>(store[CorrectionIndexKey]);
        Assert.Single(index);
        // The one persisted correction record is the one listed in the index.
        var correctionKey = store.Keys.Single(k => k.StartsWith("correction:tenant-1:", StringComparison.Ordinal));
        Assert.Equal(correctionKey, index[0]);
    }

    [Fact]
    public async Task PurgeTenantAsync_DeletesEveryCorrection_AndTheIndex()
    {
        await repository.SaveAsync(NewAudit());
        await repository.SaveAsync(NewAudit());
        await repository.SaveAsync(NewAudit());

        // A second tenant's correction + index must survive.
        const string otherKey = "correction:othertenant:req:20260101000000000:id";
        store[otherKey] = new CorrectionAuditDto { TenantId = "othertenant" };
        store["correction-index:othertenant:all"] = new List<string> { otherKey };

        var removed = await repository.PurgeTenantAsync(Tenant, CancellationToken.None);

        Assert.Equal(3, removed);
        Assert.DoesNotContain(store.Keys, k => k.Contains(Tenant, StringComparison.Ordinal));
        Assert.Contains(otherKey, store.Keys);
        Assert.Contains("correction-index:othertenant:all", store.Keys);
    }

    [Fact]
    public async Task PurgeTenantAsync_SecondCall_ReturnsZero_AndThrowsNothing()
    {
        await repository.SaveAsync(NewAudit());
        await repository.SaveAsync(NewAudit());

        Assert.Equal(2, await repository.PurgeTenantAsync(Tenant, CancellationToken.None));
        Assert.Equal(0, await repository.PurgeTenantAsync(Tenant, CancellationToken.None)); // idempotent
        Assert.Empty(store);
    }

    [Fact]
    public async Task PurgeTenantAsync_EmptyTenant_ReturnsZero()
    {
        Assert.Equal(0, await repository.PurgeTenantAsync(Tenant, CancellationToken.None));
    }

    private static CorrectionAuditDto NewAudit() => new()
    {
        Id = Guid.NewGuid(),
        RequestId = Guid.NewGuid(),
        TenantId = Tenant,
        CorrectionType = "SlotReassignment",
        OldValue = "slot-1",
        NewValue = "slot-2",
        Actor = "hr-admin",
        Reason = "manual fix",
        AppliedAt = new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc)
    };
}
