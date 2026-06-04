using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;

namespace FPS.Customer.Tests;

public sealed class TenantParkingBootstrapTests
{
    private readonly InMemoryTenantRepository tenantRepo = new();
    private readonly InMemoryTenantParkingBootstrapRepository bootstrapRepo = new();
    private readonly InMemoryTenantIdentityRepository identityRepo = new();
    private readonly TenantService tenantService;
    private readonly TenantService tenantServiceWithReadiness;
    private readonly TenantParkingBootstrapService service;

    public TenantParkingBootstrapTests()
    {
        tenantService = new TenantService(tenantRepo);
        var readiness = new TenantReadinessService(
            tenantRepo, identityRepo, bootstrapRepo,
            new NoOpProfileReadinessProbe(),
            new NoOpBookingReadinessProbe(),
            new NoOpNotificationReadinessProbe(),
            new NoOpAuditReadinessProbe(),
            new NoOpReportingReadinessProbe());
        tenantServiceWithReadiness = new TenantService(tenantRepo, readiness);
        service = new TenantParkingBootstrapService(bootstrapRepo, tenantRepo);
    }

    private async Task<string> CreateTenant(string slug = "acme")
    {
        var (t, _) = await tenantService.CreateAsync(slug, "Corp", "eu", "UTC", [], CancellationToken.None);
        return t!.TenantId;
    }

    private Task<string?> RecordPolicy(string tenantId, string actor = "actor-hash",
        string tz = "Europe/London", string cutOff = "18:00", int cap = 500, int lookback = 10) =>
        service.RecordDefaultPolicyAsync(tenantId, tz, cutOff, cap, lookback, actor, CancellationToken.None);

    // ── RecordDefaultPolicy — happy path ─────────────────────────────────────

    [Fact]
    public async Task RecordPolicy_ValidInput_SetsDefaultPolicyConfigured()
    {
        var tenantId = await CreateTenant();

        var error = await RecordPolicy(tenantId, actor: "actor-42");

        Assert.Null(error);
        var bootstrap = await service.GetAsync(tenantId, CancellationToken.None);
        Assert.True(bootstrap.DefaultPolicyConfigured);
        Assert.NotNull(bootstrap.PolicySnapshot);
        Assert.Equal("Europe/London", bootstrap.PolicySnapshot!.TimeZone);
        Assert.Equal("18:00", bootstrap.PolicySnapshot.DrawCutOffTime);
        Assert.Equal(500, bootstrap.PolicySnapshot.DailyRequestCap);
        Assert.Equal(10, bootstrap.PolicySnapshot.AllocationLookbackDays);
        Assert.Equal("actor-42", bootstrap.PolicySnapshot.RecordedByHash);
    }

    [Fact]
    public async Task RecordPolicy_Idempotent_OverwritesSnapshot()
    {
        var tenantId = await CreateTenant();
        await RecordPolicy(tenantId, tz: "UTC");

        var error = await RecordPolicy(tenantId, tz: "America/New_York", cap: 100);

        Assert.Null(error);
        var bootstrap = await service.GetAsync(tenantId, CancellationToken.None);
        Assert.Equal("America/New_York", bootstrap.PolicySnapshot!.TimeZone);
        Assert.Equal(100, bootstrap.PolicySnapshot.DailyRequestCap);
    }

    [Fact]
    public async Task RecordPolicy_UnknownTenant_ReturnsError()
    {
        var error = await RecordPolicy("no-such");

        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task RecordPolicy_ArchivedTenant_ReturnsError()
    {
        var tenantId = await CreateTenant("arch");
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Archived, "actor", null, null, CancellationToken.None);

        var error = await RecordPolicy(tenantId);

        Assert.Contains("archived", error, StringComparison.OrdinalIgnoreCase);
    }

    // ── RecordDefaultPolicy — validation ─────────────────────────────────────

    [Fact]
    public async Task RecordPolicy_EmptyTimeZone_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await RecordPolicy(tenantId, tz: "");
        Assert.Contains("TimeZone", error);
    }

    [Fact]
    public async Task RecordPolicy_InvalidCutOffTimeFormat_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await RecordPolicy(tenantId, cutOff: "6pm");
        Assert.Contains("HH:mm", error);
    }

    [Fact]
    public async Task RecordPolicy_ZeroDailyRequestCap_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await RecordPolicy(tenantId, cap: 0);
        Assert.Contains("DailyRequestCap", error);
    }

    [Fact]
    public async Task RecordPolicy_ZeroLookbackDays_IsValid()
    {
        // Configuration allows allocationLookbackDays >= 0 (non-negative), not >= 1.
        var tenantId = await CreateTenant();
        var error = await RecordPolicy(tenantId, lookback: 0);
        Assert.Null(error);
    }

    [Fact]
    public async Task RecordPolicy_NegativeLookbackDays_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await RecordPolicy(tenantId, lookback: -1);
        Assert.Contains("AllocationLookbackDays", error);
    }

    [Fact]
    public async Task RecordPolicy_DailyCapExceedsV1Limit_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await RecordPolicy(tenantId, cap: BootstrapPolicySnapshot.V1DailyRequestCapLimit + 1);
        Assert.Contains("v1 limit", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordPolicy_DrawCutOffTimeInvalidShape_ReturnsError()
    {
        // "99:99" matches HH:mm regex but is not a valid TimeOnly — must be rejected.
        var tenantId = await CreateTenant();
        var error = await RecordPolicy(tenantId, cutOff: "99:99");
        Assert.Contains("HH:mm", error);
    }

    [Fact]
    public async Task RecordPolicy_InvalidInput_DoesNotSetPolicyConfigured()
    {
        var tenantId = await CreateTenant();
        await RecordPolicy(tenantId, tz: ""); // invalid — should not persist

        var bootstrap = await service.GetAsync(tenantId, CancellationToken.None);
        Assert.False(bootstrap.DefaultPolicyConfigured);
    }

    // ── RecordLocation ───────────────────────────────────────────────────────

    [Fact]
    public async Task RecordLocation_ValidLocation_IsUsable()
    {
        var tenantId = await CreateTenant();

        var error = await service.RecordLocationAsync(tenantId, "loc-A", 10, false, "actor", CancellationToken.None);

        Assert.Null(error);
        var bootstrap = await service.GetAsync(tenantId, CancellationToken.None);
        Assert.True(bootstrap.HasUsableLocation);
        Assert.Single(bootstrap.Locations);
        Assert.Equal("loc-A", bootstrap.Locations[0].LocationId);
        Assert.Equal(10, bootstrap.Locations[0].ActiveSlotCount);
    }

    [Fact]
    public async Task RecordLocation_ZeroSlots_NotUsable()
    {
        var tenantId = await CreateTenant();
        await service.RecordLocationAsync(tenantId, "loc-empty", 0, false, "actor", CancellationToken.None);

        var bootstrap = await service.GetAsync(tenantId, CancellationToken.None);
        Assert.False(bootstrap.HasUsableLocation);
    }

    [Fact]
    public async Task RecordLocation_NegativeSlots_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await service.RecordLocationAsync(tenantId, "loc-A", -1, false, "actor", CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("negative", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordLocation_EmptyLocationId_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await service.RecordLocationAsync(tenantId, "", 5, false, "actor", CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("Location ID", error);
    }

    [Fact]
    public async Task RecordLocation_Idempotent_UpdatesExistingLocation()
    {
        var tenantId = await CreateTenant();
        await service.RecordLocationAsync(tenantId, "loc-A", 5, false, "actor", CancellationToken.None);

        await service.RecordLocationAsync(tenantId, "loc-A", 20, true, "actor-2", CancellationToken.None);

        var bootstrap = await service.GetAsync(tenantId, CancellationToken.None);
        Assert.Single(bootstrap.Locations);
        Assert.Equal(20, bootstrap.Locations[0].ActiveSlotCount);
        Assert.True(bootstrap.Locations[0].HasLocationPolicy);
    }

    [Fact]
    public async Task RecordLocation_TenantIsolation()
    {
        var t1 = await CreateTenant("corp-a");
        var t2 = await CreateTenant("corp-b");

        await RecordPolicy(t1);
        await service.RecordLocationAsync(t1, "loc-1", 5, false, "a", CancellationToken.None);

        var b2 = await service.GetAsync(t2, CancellationToken.None);
        Assert.False(b2.DefaultPolicyConfigured);
        Assert.Empty(b2.Locations);
    }

    // ── IsComplete / Ready transition guard ───────────────────────────────────

    [Fact]
    public async Task IsComplete_NoPolicyNoLocation_False()
    {
        var tenantId = await CreateTenant();
        Assert.False(await service.IsCompleteAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task IsComplete_PolicyOnly_False()
    {
        var tenantId = await CreateTenant();
        await RecordPolicy(tenantId);
        Assert.False(await service.IsCompleteAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task IsComplete_PolicyAndUsableLocation_True()
    {
        var tenantId = await CreateTenant();
        await RecordPolicy(tenantId);
        await service.RecordLocationAsync(tenantId, "loc-A", 5, false, "actor", CancellationToken.None);
        Assert.True(await service.IsCompleteAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task TransitionToReady_WithoutBootstrap_IsBlocked()
    {
        var tenantId = await CreateTenant();
        await tenantServiceWithReadiness.TransitionAsync(tenantId, TenantLifecycleState.Configured, "actor", null, null, CancellationToken.None);
        await tenantServiceWithReadiness.TransitionAsync(tenantId, TenantLifecycleState.Seeded, "actor", null, null, CancellationToken.None);

        var error = await tenantServiceWithReadiness.TransitionAsync(tenantId, TenantLifecycleState.Ready, "actor", null, null, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("cannot become Ready", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ParkingPolicy", error);
    }

    [Fact]
    public async Task TransitionToReady_PolicyButNoSlots_IsBlocked()
    {
        var tenantId = await CreateTenant();
        await tenantServiceWithReadiness.TransitionAsync(tenantId, TenantLifecycleState.Configured, "actor", null, null, CancellationToken.None);
        await tenantServiceWithReadiness.TransitionAsync(tenantId, TenantLifecycleState.Seeded, "actor", null, null, CancellationToken.None);
        await RecordPolicy(tenantId);
        await service.RecordLocationAsync(tenantId, "loc-A", 0, false, "actor", CancellationToken.None);

        var error = await tenantServiceWithReadiness.TransitionAsync(tenantId, TenantLifecycleState.Ready, "actor", null, null, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("cannot become Ready", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ParkingLocation", error);
    }

    [Fact]
    public async Task TransitionToReady_FullyBootstrapped_Succeeds()
    {
        var tenantId = await CreateTenant();
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Configured, "actor", null, null, CancellationToken.None);
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Seeded, "actor", null, null, CancellationToken.None);
        await RecordPolicy(tenantId);
        await service.RecordLocationAsync(tenantId, "loc-A", 10, false, "actor", CancellationToken.None);

        var error = await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Ready, "actor", "checks passed", null, CancellationToken.None);

        Assert.Null(error);
        var tenant = await tenantService.GetAsync(tenantId, CancellationToken.None);
        Assert.Equal(TenantLifecycleState.Ready, tenant!.LifecycleState);
    }

    [Fact]
    public async Task TransitionToSuspended_DoesNotRequireBootstrap()
    {
        var tenantId = await CreateTenant();
        var error = await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Suspended, "actor", null, null, CancellationToken.None);
        Assert.Null(error);
    }
}
