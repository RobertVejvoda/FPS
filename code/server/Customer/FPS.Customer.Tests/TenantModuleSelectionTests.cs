using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;

namespace FPS.Customer.Tests;

// PLAT007B — a tenant has exactly one primary module and may enable additional modules. These
// tests pin the invariant, the safe Parking backfill for existing tenants, and creation wiring.
public sealed class TenantModuleSelectionTests
{
    private readonly InMemoryTenantRepository repository = new();
    private readonly TenantService service;

    public TenantModuleSelectionTests() => service = new TenantService(repository);

    // ── Domain invariant ──────────────────────────────────────────────────────

    [Fact]
    public void NewTenant_DefaultsToParkingPrimary_ParkingOnlyEnabled()
    {
        var tenant = new TenantWorkspace { TenantId = "t", Slug = "t" };

        Assert.Equal(TenantModule.Parking, tenant.PrimaryModule);
        Assert.Equal([TenantModule.Parking], tenant.EnabledModules);
    }

    [Fact]
    public void SetModules_SeatsPrimaryWithBothEnabled_StoresPrimaryFirst()
    {
        var tenant = new TenantWorkspace { TenantId = "t", Slug = "t" };

        var error = tenant.SetModules(TenantModule.Seats, [TenantModule.Parking, TenantModule.Seats]);

        Assert.Null(error);
        Assert.Equal(TenantModule.Seats, tenant.PrimaryModule);
        // Primary is always first, regardless of the order it was supplied in.
        Assert.Equal([TenantModule.Seats, TenantModule.Parking], tenant.EnabledModules);
    }

    [Fact]
    public void SetModules_EmptyEnabled_DefaultsToPrimaryOnly()
    {
        var tenant = new TenantWorkspace { TenantId = "t", Slug = "t" };

        var error = tenant.SetModules(TenantModule.Seats, []);

        Assert.Null(error);
        Assert.Equal([TenantModule.Seats], tenant.EnabledModules);
    }

    [Fact]
    public void SetModules_DuplicateEnabled_CollapsedAndPrimaryFirst()
    {
        var tenant = new TenantWorkspace { TenantId = "t", Slug = "t" };

        tenant.SetModules(TenantModule.Parking, [TenantModule.Parking, TenantModule.Seats, TenantModule.Parking]);

        Assert.Equal([TenantModule.Parking, TenantModule.Seats], tenant.EnabledModules);
    }

    [Fact]
    public void SetModules_PrimaryNotInEnabled_ReturnsError_AndDoesNotMutate()
    {
        var tenant = new TenantWorkspace { TenantId = "t", Slug = "t" };

        var error = tenant.SetModules(TenantModule.Seats, [TenantModule.Parking]);

        Assert.NotNull(error);
        Assert.Contains("primary module", error);
        // Unchanged from the default on rejection.
        Assert.Equal(TenantModule.Parking, tenant.PrimaryModule);
        Assert.Equal([TenantModule.Parking], tenant.EnabledModules);
    }

    [Fact]
    public void SetModules_UndefinedPrimary_ReturnsError_AndDoesNotMutate()
    {
        var tenant = new TenantWorkspace { TenantId = "t", Slug = "t" };

        // A numeric value that Enum.TryParse would accept but is not a defined module.
        var error = tenant.SetModules((TenantModule)999, [(TenantModule)999]);

        Assert.NotNull(error);
        Assert.Contains("Unknown module", error);
        Assert.Equal(TenantModule.Parking, tenant.PrimaryModule);
        Assert.Equal([TenantModule.Parking], tenant.EnabledModules);
    }

    [Fact]
    public void SetModules_UndefinedInEnabled_ReturnsError()
    {
        var tenant = new TenantWorkspace { TenantId = "t", Slug = "t" };

        var error = tenant.SetModules(TenantModule.Parking, [TenantModule.Parking, (TenantModule)42]);

        Assert.NotNull(error);
        Assert.Contains("Unknown module", error);
    }

    // ── Persistence backfill ──────────────────────────────────────────────────

    [Fact]
    public void Restore_LegacyDtoWithoutModules_BackfillsToParking()
    {
        // A tenant persisted before PLAT007B: PrimaryModule deserialises as Parking (enum 0) and
        // EnabledModules deserialises empty.
        var legacy = new TenantWorkspaceDto
        {
            TenantId = "legacy", Slug = "legacy", DisplayName = "Legacy", Region = "eu",
            TimeZone = "Europe/Prague", EnabledModules = [],
        };

        var tenant = legacy.ToDomain();

        Assert.Equal(TenantModule.Parking, tenant.PrimaryModule);
        Assert.Equal([TenantModule.Parking], tenant.EnabledModules);
    }

    [Fact]
    public void Dto_RoundTrip_PreservesModuleSelection()
    {
        var tenant = new TenantWorkspace { TenantId = "t", Slug = "t" };
        tenant.SetModules(TenantModule.Seats, [TenantModule.Parking, TenantModule.Seats]);

        var restored = TenantWorkspaceDto.FromDomain(tenant).ToDomain();

        Assert.Equal(TenantModule.Seats, restored.PrimaryModule);
        Assert.Equal([TenantModule.Seats, TenantModule.Parking], restored.EnabledModules);
    }

    // ── Creation wiring ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithoutModules_DefaultsToParking()
    {
        var (tenant, error) = await service.CreateAsync(
            "acme", "ACME", "eu", "Europe/London", [], CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(TenantModule.Parking, tenant!.PrimaryModule);
        Assert.Equal([TenantModule.Parking], tenant.EnabledModules);
    }

    [Fact]
    public async Task Create_WithSeatsPrimary_PersistsSelection()
    {
        var (tenant, error) = await service.CreateAsync(
            "desks", "Desks Co", "eu", "Europe/London", [], CancellationToken.None,
            primaryModule: TenantModule.Seats,
            enabledModules: [TenantModule.Parking, TenantModule.Seats]);

        Assert.Null(error);
        var stored = await repository.GetAsync(tenant!.TenantId, CancellationToken.None);
        Assert.Equal(TenantModule.Seats, stored!.PrimaryModule);
        Assert.Equal([TenantModule.Seats, TenantModule.Parking], stored.EnabledModules);
    }

    [Fact]
    public async Task Create_PrimaryNotInEnabled_FailsWithoutPersisting()
    {
        var (tenant, error) = await service.CreateAsync(
            "bad", "Bad Co", "eu", "Europe/London", [], CancellationToken.None,
            primaryModule: TenantModule.Seats,
            enabledModules: [TenantModule.Parking]);

        Assert.Null(tenant);
        Assert.NotNull(error);
        Assert.Contains("primary module", error);
    }

    [Fact]
    public async Task Create_UndefinedModule_FailsWithoutPersisting()
    {
        var (tenant, error) = await service.CreateAsync(
            "bad", "Bad Co", "eu", "Europe/London", [], CancellationToken.None,
            primaryModule: (TenantModule)999);

        Assert.Null(tenant);
        Assert.NotNull(error);
        Assert.Contains("Unknown module", error);
    }
}
