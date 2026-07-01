using FPS.Reporting.Infrastructure;

namespace FPS.Reporting.Tests;

public sealed class InMemoryReportingRepositoryPurgeTests
{
    private readonly InMemoryReportingRepository repository = new();

    [Fact]
    public async Task PurgeTenantAsync_RemovesAllStateForTenant_LeavesOtherTenantIntact()
    {
        // Tenant A: one metric row, one fairness row, one seen-event marker.
        await repository.ApplyMetricsAsync("A", "2026-06-01", "loc-1", "09:00-17:00", m => m.IncrementDemand());
        await repository.ApplyFairnessAsync("A", "user-a", "2026-06-01", "loc-1", f => f.IncrementRequest());
        await repository.RecordEventIdAsync("A", "evt-a");

        // Tenant B: independent state that must survive the purge.
        await repository.ApplyMetricsAsync("B", "2026-06-01", "loc-1", "09:00-17:00", m => m.IncrementDemand());
        await repository.ApplyFairnessAsync("B", "user-b", "2026-06-01", "loc-1", f => f.IncrementRequest());
        await repository.RecordEventIdAsync("B", "evt-b");

        var removed = await repository.PurgeTenantAsync("A");

        // One metric + one fairness row removed for A.
        Assert.Equal(2, removed);

        // A's reads are now empty and its dedup marker is gone.
        Assert.Empty(await repository.QueryMetricsAsync(new(), "A"));
        Assert.Empty(await repository.QueryFairnessAsync(new(), "A"));
        Assert.False(await repository.EventExistsAsync("evt-a"));

        // B is fully intact.
        Assert.Single(await repository.QueryMetricsAsync(new(), "B"));
        Assert.Single(await repository.QueryFairnessAsync(new(), "B"));
        Assert.True(await repository.EventExistsAsync("evt-b"));
    }

    [Fact]
    public async Task PurgeTenantAsync_IsIdempotent_SecondPurgeReturnsZero()
    {
        await repository.ApplyMetricsAsync("A", "2026-06-01", "loc-1", "09:00-17:00", m => m.IncrementDemand());
        await repository.ApplyFairnessAsync("A", "user-a", "2026-06-01", "loc-1", f => f.IncrementRequest());

        var first = await repository.PurgeTenantAsync("A");
        var second = await repository.PurgeTenantAsync("A");

        Assert.Equal(2, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task PurgeTenantAsync_UnknownTenant_ReturnsZero()
    {
        var removed = await repository.PurgeTenantAsync("does-not-exist");

        Assert.Equal(0, removed);
    }
}
