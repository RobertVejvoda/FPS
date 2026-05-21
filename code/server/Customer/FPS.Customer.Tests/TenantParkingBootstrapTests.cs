using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;

namespace FPS.Customer.Tests;

public sealed class TenantParkingBootstrapTests
{
    private readonly InMemoryTenantRepository tenantRepo = new();
    private readonly InMemoryTenantParkingBootstrapRepository bootstrapRepo = new();
    private readonly TenantService tenantService;
    private readonly TenantParkingBootstrapService service;

    public TenantParkingBootstrapTests()
    {
        tenantService = new TenantService(tenantRepo, bootstrapRepo);
        service = new TenantParkingBootstrapService(bootstrapRepo, tenantRepo);
    }

    private async Task<string> CreateTenant(string slug = "acme")
    {
        var (t, _) = await tenantService.CreateAsync(slug, "Corp", "eu", "UTC", [], CancellationToken.None);
        return t!.TenantId;
    }

    // ── RecordDefaultPolicy ──────────────────────────────────────────────────

    [Fact]
    public async Task RecordPolicy_ValidTenant_SetsDefaultPolicyConfigured()
    {
        var tenantId = await CreateTenant();

        var error = await service.RecordDefaultPolicyAsync(tenantId, "actor-hash", CancellationToken.None);

        Assert.Null(error);
        var bootstrap = await service.GetAsync(tenantId, CancellationToken.None);
        Assert.True(bootstrap.DefaultPolicyConfigured);
        Assert.Equal("actor-hash", bootstrap.PolicyRecordedByHash);
    }

    [Fact]
    public async Task RecordPolicy_UnknownTenant_ReturnsError()
    {
        var error = await service.RecordDefaultPolicyAsync("no-such", "actor", CancellationToken.None);

        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task RecordPolicy_ArchivedTenant_ReturnsError()
    {
        var tenantId = await CreateTenant("arch");
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Archived, "actor", null, null, CancellationToken.None);

        var error = await service.RecordDefaultPolicyAsync(tenantId, "actor", CancellationToken.None);

        Assert.Contains("archived", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordPolicy_Idempotent_CanBeCalledAgain()
    {
        var tenantId = await CreateTenant();
        await service.RecordDefaultPolicyAsync(tenantId, "actor-1", CancellationToken.None);

        var error = await service.RecordDefaultPolicyAsync(tenantId, "actor-2", CancellationToken.None);

        Assert.Null(error);
        var bootstrap = await service.GetAsync(tenantId, CancellationToken.None);
        Assert.Equal("actor-2", bootstrap.PolicyRecordedByHash);
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

        await service.RecordDefaultPolicyAsync(t1, "a", CancellationToken.None);
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
        await service.RecordDefaultPolicyAsync(tenantId, "actor", CancellationToken.None);

        Assert.False(await service.IsCompleteAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task IsComplete_PolicyAndUsableLocation_True()
    {
        var tenantId = await CreateTenant();
        await service.RecordDefaultPolicyAsync(tenantId, "actor", CancellationToken.None);
        await service.RecordLocationAsync(tenantId, "loc-A", 5, false, "actor", CancellationToken.None);

        Assert.True(await service.IsCompleteAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task TransitionToReady_WithoutBootstrap_IsBlocked()
    {
        var tenantId = await CreateTenant();
        // Walk the lifecycle forward to Seeded
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Configured, "actor", null, null, CancellationToken.None);
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Seeded, "actor", null, null, CancellationToken.None);

        var error = await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Ready, "actor", null, null, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("parking policy", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransitionToReady_PolicyButNoSlots_IsBlocked()
    {
        var tenantId = await CreateTenant();
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Configured, "actor", null, null, CancellationToken.None);
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Seeded, "actor", null, null, CancellationToken.None);
        await service.RecordDefaultPolicyAsync(tenantId, "actor", CancellationToken.None);
        await service.RecordLocationAsync(tenantId, "loc-A", 0, false, "actor", CancellationToken.None); // zero slots

        var error = await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Ready, "actor", null, null, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("slots", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransitionToReady_FullyBootstrapped_Succeeds()
    {
        var tenantId = await CreateTenant();
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Configured, "actor", null, null, CancellationToken.None);
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Seeded, "actor", null, null, CancellationToken.None);
        await service.RecordDefaultPolicyAsync(tenantId, "actor", CancellationToken.None);
        await service.RecordLocationAsync(tenantId, "loc-A", 10, false, "actor", CancellationToken.None);

        var error = await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Ready, "actor", "all checks passed", null, CancellationToken.None);

        Assert.Null(error);
        var tenant = await tenantService.GetAsync(tenantId, CancellationToken.None);
        Assert.Equal(TenantLifecycleState.Ready, tenant!.LifecycleState);
    }

    [Fact]
    public async Task TransitionToSuspended_DoesNotRequireBootstrap()
    {
        // Bootstrap guard only applies to Ready, not to other transitions.
        var tenantId = await CreateTenant();

        var error = await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Suspended, "actor", null, null, CancellationToken.None);

        Assert.Null(error);
    }
}
