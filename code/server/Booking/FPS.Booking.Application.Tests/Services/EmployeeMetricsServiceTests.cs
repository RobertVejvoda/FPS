using FPS.Booking.Infrastructure.Services;

namespace FPS.Booking.Application.Tests.Services;

public sealed class EmployeeMetricsServiceTests
{
    private readonly InMemoryEmployeeMetricsService sut = new();
    private const string TenantId = "tenant-1";
    private const string RequestorId = "user-1";
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task GetMetrics_NoHistory_ReturnsZeroCount()
    {
        var result = await sut.GetMetricsSnapshotAsync(TenantId, [RequestorId], Today, lookbackDays: 10);

        Assert.Equal(0, result[RequestorId].RecentAllocationCount);
    }

    [Fact]
    public async Task GetMetrics_AllocationWithinWindow_CountsIt()
    {
        await sut.IncrementRecentAllocationAsync(TenantId, RequestorId, Today.AddDays(-5));

        var result = await sut.GetMetricsSnapshotAsync(TenantId, [RequestorId], Today, lookbackDays: 10);

        Assert.Equal(1, result[RequestorId].RecentAllocationCount);
    }

    [Fact]
    public async Task GetMetrics_AllocationOutsideWindow_DoesNotCount()
    {
        await sut.IncrementRecentAllocationAsync(TenantId, RequestorId, Today.AddDays(-15));

        var result = await sut.GetMetricsSnapshotAsync(TenantId, [RequestorId], Today, lookbackDays: 10);

        Assert.Equal(0, result[RequestorId].RecentAllocationCount);
    }

    [Fact]
    public async Task GetMetrics_MultipleAllocations_CountsOnlyWithinWindow()
    {
        await sut.IncrementRecentAllocationAsync(TenantId, RequestorId, Today.AddDays(-2));
        await sut.IncrementRecentAllocationAsync(TenantId, RequestorId, Today.AddDays(-8));
        await sut.IncrementRecentAllocationAsync(TenantId, RequestorId, Today.AddDays(-12));

        var result = await sut.GetMetricsSnapshotAsync(TenantId, [RequestorId], Today, lookbackDays: 10);

        Assert.Equal(2, result[RequestorId].RecentAllocationCount);
    }

    [Fact]
    public async Task GetMetrics_DifferentTenants_Isolated()
    {
        await sut.IncrementRecentAllocationAsync("tenant-A", RequestorId, Today);

        var result = await sut.GetMetricsSnapshotAsync("tenant-B", [RequestorId], Today, lookbackDays: 10);

        Assert.Equal(0, result[RequestorId].RecentAllocationCount);
    }

    [Fact]
    public async Task GetMetrics_Tier2Weight_DecreasesWithMoreAllocations()
    {
        await sut.IncrementRecentAllocationAsync(TenantId, RequestorId, Today.AddDays(-1));
        await sut.IncrementRecentAllocationAsync(TenantId, RequestorId, Today.AddDays(-2));
        await sut.IncrementRecentAllocationAsync(TenantId, RequestorId, Today.AddDays(-3));

        var result = await sut.GetMetricsSnapshotAsync(TenantId, [RequestorId], Today, lookbackDays: 10);
        var metrics = result[RequestorId];

        // weight = 1 / (1 + 3 + 0) = 0.25
        Assert.Equal(0.25, metrics.Tier2Weight, precision: 5);
    }

    [Fact]
    public async Task GetMetrics_UnknownRequestor_ReturnsZeroMetrics()
    {
        var result = await sut.GetMetricsSnapshotAsync(TenantId, ["unknown-user"], Today, lookbackDays: 10);

        Assert.Equal(0, result["unknown-user"].RecentAllocationCount);
        Assert.Equal(0, result["unknown-user"].ActivePenaltyScore);
    }
}
