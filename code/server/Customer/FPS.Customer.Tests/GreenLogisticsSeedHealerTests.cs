using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Tests;

// #719 — the GL seeder only enabled Seats on first creation, so a tenant seeded before #710 stayed
// Parking-only and Booking rejected seat requests with 422 RequestorIneligible. These tests pin the
// in-place heal (Seats + PLAT003A resettable flag), scoped to sandbox tenants and idempotent.
public sealed class GreenLogisticsSeedHealerTests
{
    // A GL sandbox tenant seeded before module selection existed: it restores as Parking-only with
    // the resettable flag unset — exactly the state the two repairs target.
    private static TenantWorkspace Pre710SandboxTenant() => new()
    {
        TenantId = "greenlogistics",
        Slug = "greenlogistics",
        DisplayName = "Green Logistics",
        Region = "EU",
        TimeZone = "Europe/Prague",
        Kind = TenantKind.Sandbox,
        Provisioning = TenantProvisioningMetadata.Generate("greenlogistics", "greenlogistics"),
    };

    [Fact]
    public void Heal_Pre710SandboxTenant_EnablesSeatsInPlace()
    {
        var tenant = Pre710SandboxTenant();
        Assert.DoesNotContain(TenantModule.Seats, tenant.EnabledModules); // sanity: starts Parking-only

        var (healed, changed) = GreenLogisticsSeedHealer.Heal(tenant);

        Assert.True(changed);
        Assert.Equal(TenantModule.Parking, healed.PrimaryModule);
        Assert.Equal([TenantModule.Parking, TenantModule.Seats], healed.EnabledModules);
    }

    [Fact]
    public void Heal_Pre710SandboxTenant_RestoresResettableSandboxFlag()
    {
        var tenant = Pre710SandboxTenant();
        Assert.False(tenant.IsResettableSandbox); // sanity: flag predates PLAT003A

        var (healed, changed) = GreenLogisticsSeedHealer.Heal(tenant);

        Assert.True(changed);
        Assert.True(healed.IsResettableSandbox);
    }

    [Fact]
    public void Heal_AlreadyHealedTenant_ReportsNoChange()
    {
        var tenant = new TenantWorkspace
        {
            TenantId = "greenlogistics",
            Slug = "greenlogistics",
            Kind = TenantKind.Sandbox,
            IsResettableSandbox = true,
        };
        tenant.SetModules(TenantModule.Parking, [TenantModule.Parking, TenantModule.Seats]);

        var (healed, changed) = GreenLogisticsSeedHealer.Heal(tenant);

        Assert.False(changed);
        Assert.Same(tenant, healed);
        Assert.Equal([TenantModule.Parking, TenantModule.Seats], healed.EnabledModules);
    }

    [Fact]
    public void Heal_SeatsPresentButFlagMissing_RepairsOnlyTheFlagAndKeepsSeats()
    {
        var tenant = Pre710SandboxTenant();
        tenant.SetModules(TenantModule.Parking, [TenantModule.Parking, TenantModule.Seats]);
        Assert.False(tenant.IsResettableSandbox);

        var (healed, changed) = GreenLogisticsSeedHealer.Heal(tenant);

        Assert.True(changed);
        Assert.True(healed.IsResettableSandbox);
        Assert.Equal([TenantModule.Parking, TenantModule.Seats], healed.EnabledModules);
    }

    [Fact]
    public void Heal_NonSandboxTenant_LeavesModulesUntouched()
    {
        var tenant = new TenantWorkspace
        {
            TenantId = "prod-co",
            Slug = "prod-co",
            Kind = TenantKind.Production,
        };

        var (healed, changed) = GreenLogisticsSeedHealer.Heal(tenant);

        Assert.False(changed);
        Assert.DoesNotContain(TenantModule.Seats, healed.EnabledModules);
        Assert.False(healed.IsResettableSandbox);
    }
}
