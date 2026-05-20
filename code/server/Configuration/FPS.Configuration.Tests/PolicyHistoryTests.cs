using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;

namespace FPS.Configuration.Tests;

public sealed class PolicyHistoryTests
{
    private static ParkingPolicy MakePolicy(string tenantId, string? locationId = null, string actor = "admin-1") =>
        new()
        {
            TenantId = tenantId,
            LocationId = locationId,
            TimeZone = "Europe/Prague",
            DrawCutOffTime = new TimeOnly(18, 0),
            DailyRequestCap = 100,
            AllocationLookbackDays = 10,
            LateCancellationPenalty = 1,
            NoShowPenalty = 2,
            ManualAdjustmentEnabled = true,
            SameDayBookingEnabled = true,
            SameDayUsesRequestCap = true,
            AutomaticReallocationEnabled = true,
            CompanyCarTier1Enabled = true,
            CompanyCarOverflowBehavior = "reject",
            PublishedByUserId = actor,
            PublishedAt = DateTimeOffset.UtcNow,
        };

    // ── GetHistoryAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task History_EmptyWhenNoPolicySaved()
    {
        var repo = new InMemoryParkingPolicyRepository();
        var history = await repo.GetHistoryAsync("tenant-1", null);
        Assert.Empty(history);
    }

    [Fact]
    public async Task History_AccumulatesVersions()
    {
        var repo = new InMemoryParkingPolicyRepository();
        var v1 = MakePolicy("tenant-1") with { DailyRequestCap = 50 };
        var v2 = MakePolicy("tenant-1") with { DailyRequestCap = 100 };

        await repo.SaveAsync(v1);
        await repo.SaveAsync(v2);

        var history = await repo.GetHistoryAsync("tenant-1", null);
        Assert.Equal(2, history.Count);
        Assert.Equal(100, history[0].DailyRequestCap);  // newest first
        Assert.Equal(50, history[1].DailyRequestCap);
    }

    [Fact]
    public async Task History_CurrentPolicyIsLatestVersion()
    {
        var repo = new InMemoryParkingPolicyRepository();
        await repo.SaveAsync(MakePolicy("tenant-1") with { DailyRequestCap = 50 });
        await repo.SaveAsync(MakePolicy("tenant-1") with { DailyRequestCap = 99 });

        var current = await repo.GetTenantDefaultAsync("tenant-1");
        Assert.Equal(99, current!.DailyRequestCap);
    }

    [Fact]
    public async Task History_TenantScoped()
    {
        var repo = new InMemoryParkingPolicyRepository();
        await repo.SaveAsync(MakePolicy("tenant-A"));
        await repo.SaveAsync(MakePolicy("tenant-A"));

        var historyB = await repo.GetHistoryAsync("tenant-B", null);
        Assert.Empty(historyB);
    }

    [Fact]
    public async Task History_LocationScopedSeparatelyFromDefault()
    {
        var repo = new InMemoryParkingPolicyRepository();
        await repo.SaveAsync(MakePolicy("tenant-1"));
        await repo.SaveAsync(MakePolicy("tenant-1", "loc-1"));

        var defaultHistory = await repo.GetHistoryAsync("tenant-1", null);
        var locationHistory = await repo.GetHistoryAsync("tenant-1", "loc-1");

        Assert.Single(defaultHistory);
        Assert.Single(locationHistory);
    }

    [Fact]
    public async Task History_LimitIsApplied()
    {
        var repo = new InMemoryParkingPolicyRepository();
        for (var i = 0; i < 10; i++)
            await repo.SaveAsync(MakePolicy("tenant-1") with { DailyRequestCap = i + 1 });

        var history = await repo.GetHistoryAsync("tenant-1", null, limit: 3);
        Assert.Equal(3, history.Count);
        Assert.Equal(10, history[0].DailyRequestCap);  // newest first
    }

    [Fact]
    public async Task History_PreservesActorAndPublishedAt()
    {
        var repo = new InMemoryParkingPolicyRepository();
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        var policy = MakePolicy("tenant-1", actor: "hr-manager-1");
        await repo.SaveAsync(policy);

        var history = await repo.GetHistoryAsync("tenant-1", null);
        Assert.Equal("hr-manager-1", history[0].PublishedByUserId);
        Assert.True(history[0].PublishedAt >= before);
    }

    [Fact]
    public async Task History_PreservesPublicationReason()
    {
        var repo = new InMemoryParkingPolicyRepository();
        await repo.SaveAsync(MakePolicy("tenant-1") with { PublicationReason = "Q3 policy review" });

        var history = await repo.GetHistoryAsync("tenant-1", null);
        Assert.Equal("Q3 policy review", history[0].PublicationReason);
    }
}
