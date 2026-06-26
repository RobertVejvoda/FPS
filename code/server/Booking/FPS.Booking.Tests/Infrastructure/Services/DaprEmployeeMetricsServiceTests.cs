using Dapr.Client;
using FPS.Booking.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FPS.Booking.Infrastructure.Tests.Services;

public sealed class DaprEmployeeMetricsServiceTests
{
    private const string StoreName = "bookingstore";
    private const string TenantId = "tenant-abc";
    private const string UserId = "user-001";
    private static readonly DateOnly Today = new(2026, 6, 15);

    private readonly Dictionary<string, object?> store = new();

    private DaprEmployeeMetricsService BuildService(Mock<IPenaltyRepository>? penaltyMock = null)
    {
        penaltyMock ??= EmptyPenaltyMock();

        var daprMock = new Mock<DaprClient>();

        daprMock.Setup(c => c.GetStateAsync<List<string>>(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as List<string> : (List<string>?)null);

        daprMock.Setup(c => c.SaveStateAsync(
                StoreName, It.IsAny<string>(), It.IsAny<List<string>>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<string>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = new List<string>(value))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection()
            .AddScoped(_ => penaltyMock.Object)
            .BuildServiceProvider();

        return new DaprEmployeeMetricsService(daprMock.Object, services.GetRequiredService<IServiceScopeFactory>());
    }

    private static Mock<IPenaltyRepository> EmptyPenaltyMock()
    {
        var mock = new Mock<IPenaltyRepository>();
        mock.Setup(r => r.GetActiveByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return mock;
    }

    // ── Restart persistence ───────────────────────────────────────────────────

    [Fact]
    public async Task IncrementThenRestart_GetMetrics_ReturnsDurableCount()
    {
        var svc1 = BuildService();
        await svc1.IncrementRecentAllocationAsync(TenantId, UserId, Today.AddDays(-3));

        // Simulate restart by building a new service that shares the same backing store.
        var svc2 = BuildService();
        var result = await svc2.GetMetricsSnapshotAsync(TenantId, [UserId], Today, lookbackDays: 10);

        Assert.Equal(1, result[UserId].RecentAllocationCount);
    }

    // ── Lookback window filtering ─────────────────────────────────────────────

    [Fact]
    public async Task GetMetrics_AllocationWithinWindow_Counted()
    {
        var svc = BuildService();
        await svc.IncrementRecentAllocationAsync(TenantId, UserId, Today.AddDays(-5));

        var result = await svc.GetMetricsSnapshotAsync(TenantId, [UserId], Today, lookbackDays: 10);

        Assert.Equal(1, result[UserId].RecentAllocationCount);
    }

    [Fact]
    public async Task GetMetrics_AllocationOutsideWindow_NotCounted()
    {
        var svc = BuildService();
        await svc.IncrementRecentAllocationAsync(TenantId, UserId, Today.AddDays(-15));

        var result = await svc.GetMetricsSnapshotAsync(TenantId, [UserId], Today, lookbackDays: 10);

        Assert.Equal(0, result[UserId].RecentAllocationCount);
    }

    [Fact]
    public async Task GetMetrics_MixedAllocations_CountsOnlyWithinWindow()
    {
        var svc = BuildService();
        await svc.IncrementRecentAllocationAsync(TenantId, UserId, Today.AddDays(-2));
        await svc.IncrementRecentAllocationAsync(TenantId, UserId, Today.AddDays(-8));
        await svc.IncrementRecentAllocationAsync(TenantId, UserId, Today.AddDays(-12));

        var result = await svc.GetMetricsSnapshotAsync(TenantId, [UserId], Today, lookbackDays: 10);

        Assert.Equal(2, result[UserId].RecentAllocationCount);
    }

    [Fact]
    public async Task GetMetrics_NoHistory_ReturnsZero()
    {
        var svc = BuildService();

        var result = await svc.GetMetricsSnapshotAsync(TenantId, [UserId], Today, lookbackDays: 10);

        Assert.Equal(0, result[UserId].RecentAllocationCount);
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetMetrics_DifferentTenants_Isolated()
    {
        var svc = BuildService();
        await svc.IncrementRecentAllocationAsync("tenant-aaa", UserId, Today);

        var result = await svc.GetMetricsSnapshotAsync("tenant-bbb", [UserId], Today, lookbackDays: 10);

        Assert.Equal(0, result[UserId].RecentAllocationCount);
    }

    [Fact]
    public async Task GetMetrics_TwoTenants_EachSeeOwnCounts()
    {
        var svc = BuildService();
        await svc.IncrementRecentAllocationAsync("tenant-aaa", UserId, Today.AddDays(-1));
        await svc.IncrementRecentAllocationAsync("tenant-aaa", UserId, Today.AddDays(-2));
        await svc.IncrementRecentAllocationAsync("tenant-bbb", UserId, Today.AddDays(-1));

        var resultA = await svc.GetMetricsSnapshotAsync("tenant-aaa", [UserId], Today, lookbackDays: 10);
        var resultB = await svc.GetMetricsSnapshotAsync("tenant-bbb", [UserId], Today, lookbackDays: 10);

        Assert.Equal(2, resultA[UserId].RecentAllocationCount);
        Assert.Equal(1, resultB[UserId].RecentAllocationCount);
    }

    // ── Active penalty score ──────────────────────────────────────────────────

    [Fact]
    public async Task GetMetrics_IncludesActivePenaltyScore()
    {
        var penaltyMock = new Mock<IPenaltyRepository>();
        penaltyMock.Setup(r => r.GetActiveByRequestorAsync(TenantId, UserId, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PenaltyDto { Score = 3, ExpiryDate = Today.AddDays(5) }]);

        var svc = BuildService(penaltyMock);
        var result = await svc.GetMetricsSnapshotAsync(TenantId, [UserId], Today, lookbackDays: 10);

        Assert.Equal(3, result[UserId].ActivePenaltyScore);
    }

    [Fact]
    public async Task GetMetrics_ExpiredPenalty_NotCounted()
    {
        var penaltyMock = new Mock<IPenaltyRepository>();
        penaltyMock.Setup(r => r.GetActiveByRequestorAsync(TenantId, UserId, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PenaltyDto { Score = 5, ExpiryDate = Today.AddDays(-1) }]);

        var svc = BuildService(penaltyMock);
        var result = await svc.GetMetricsSnapshotAsync(TenantId, [UserId], Today, lookbackDays: 10);

        Assert.Equal(0, result[UserId].ActivePenaltyScore);
    }

    // ── Tier2Weight ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMetrics_Tier2Weight_DecreasesWithAllocationCount()
    {
        var svc = BuildService();
        await svc.IncrementRecentAllocationAsync(TenantId, UserId, Today.AddDays(-1));
        await svc.IncrementRecentAllocationAsync(TenantId, UserId, Today.AddDays(-2));
        await svc.IncrementRecentAllocationAsync(TenantId, UserId, Today.AddDays(-3));

        var result = await svc.GetMetricsSnapshotAsync(TenantId, [UserId], Today, lookbackDays: 10);

        // 1 / (1 + 3 + 0) = 0.25
        Assert.Equal(0.25, result[UserId].Tier2Weight, precision: 5);
    }

    // ── Multiple participants ─────────────────────────────────────────────────

    [Fact]
    public async Task GetMetrics_MultipleParticipants_EachCorrect()
    {
        var svc = BuildService();
        await svc.IncrementRecentAllocationAsync(TenantId, "user-001", Today.AddDays(-1));
        await svc.IncrementRecentAllocationAsync(TenantId, "user-001", Today.AddDays(-2));
        await svc.IncrementRecentAllocationAsync(TenantId, "user-002", Today.AddDays(-1));

        var result = await svc.GetMetricsSnapshotAsync(TenantId, ["user-001", "user-002"], Today, lookbackDays: 10);

        Assert.Equal(2, result["user-001"].RecentAllocationCount);
        Assert.Equal(1, result["user-002"].RecentAllocationCount);
    }
}
