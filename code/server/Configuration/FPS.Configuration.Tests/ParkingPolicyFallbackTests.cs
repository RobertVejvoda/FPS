using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;

namespace FPS.Configuration.Tests;

public sealed class ParkingPolicyFallbackTests
{
    private static ParkingPolicy MakePolicy(string tenantId, string? locationId = null) => new()
    {
        TenantId = tenantId,
        LocationId = locationId,
        TimeZone = locationId is null ? "Europe/Prague" : "Europe/Berlin",
        DrawCutOffTime = new TimeOnly(18, 0),
        DailyRequestCap = 100,
        AllocationLookbackDays = 10,
        LateCancellationPenalty = 1,
        NoShowPenalty = 2,
        UsageConfirmationMethods = [],
        CompanyCarOverflowBehavior = "reject",
        PublishedByUserId = "user-1",
        PublishedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task GetEffectivePolicy_LocationOverrideExists_ReturnsLocationPolicy()
    {
        var repo = new InMemoryParkingPolicyRepository();
        var service = new ParkingPolicyService(repo);

        var tenantDefault = MakePolicy("tenant-1");
        var locationOverride = MakePolicy("tenant-1", "loc-A");

        await repo.SaveAsync(tenantDefault);
        await repo.SaveAsync(locationOverride);

        var effective = await service.GetEffectivePolicyAsync("tenant-1", "loc-A", default);

        Assert.NotNull(effective);
        Assert.Equal("loc-A", effective.LocationId);
        Assert.Equal("Europe/Berlin", effective.TimeZone);
    }

    [Fact]
    public async Task GetEffectivePolicy_NoLocationOverride_ReturnsTenantDefault()
    {
        var repo = new InMemoryParkingPolicyRepository();
        var service = new ParkingPolicyService(repo);

        var tenantDefault = MakePolicy("tenant-1");
        await repo.SaveAsync(tenantDefault);

        var effective = await service.GetEffectivePolicyAsync("tenant-1", "loc-B", default);

        Assert.NotNull(effective);
        Assert.Null(effective.LocationId);
        Assert.Equal("Europe/Prague", effective.TimeZone);
    }

    [Fact]
    public async Task GetEffectivePolicy_NeitherExists_ReturnsNull()
    {
        var repo = new InMemoryParkingPolicyRepository();
        var service = new ParkingPolicyService(repo);

        var effective = await service.GetEffectivePolicyAsync("tenant-1", "loc-X", default);

        Assert.Null(effective);
    }

    [Fact]
    public async Task GetTenantDefault_WhenSaved_ReturnsCorrectPolicy()
    {
        var repo = new InMemoryParkingPolicyRepository();
        var service = new ParkingPolicyService(repo);

        var policy = MakePolicy("tenant-1");
        await service.SaveTenantDefaultAsync(policy, default);

        var result = await service.GetTenantDefaultAsync("tenant-1", default);
        Assert.NotNull(result);
        Assert.Equal("tenant-1", result.TenantId);
    }

    [Fact]
    public async Task TenantIsolation_TenantACannotSeeTenantBPolicy()
    {
        var repo = new InMemoryParkingPolicyRepository();
        var service = new ParkingPolicyService(repo);

        await repo.SaveAsync(MakePolicy("tenant-A"));

        var result = await service.GetTenantDefaultAsync("tenant-B", default);
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveLocationOverride_InvalidPolicy_ReturnsErrors()
    {
        var repo = new InMemoryParkingPolicyRepository();
        var service = new ParkingPolicyService(repo);

        var invalid = MakePolicy("tenant-1", "loc-X") with { TimeZone = "" };
        var errors = await service.SaveLocationOverrideAsync(invalid, default);

        Assert.NotEmpty(errors);
        var stored = await repo.GetLocationOverrideAsync("tenant-1", "loc-X");
        Assert.Null(stored);
    }
}
