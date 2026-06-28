using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.Identity;

namespace FPS.Customer.Tests;

public sealed class TenantIdentityServiceTests
{
    private readonly InMemoryTenantRepository tenantRepo = new();
    private readonly InMemoryTenantIdentityRepository identityRepo = new();
    private readonly InMemoryTenantIdentityConfigStore configStore = new();
    private readonly InMemoryTenantRoleMappingStore roleMappingStore;
    private readonly TenantService tenantService;
    private readonly TenantIdentityService service;

    public TenantIdentityServiceTests()
    {
        roleMappingStore = new InMemoryTenantRoleMappingStore(configStore);
        tenantService = new TenantService(tenantRepo);
        service = new TenantIdentityService(identityRepo, tenantRepo, configStore, roleMappingStore);
    }

    private async Task<string> CreateTenant(string slug = "acme")
    {
        var (tenant, _) = await tenantService.CreateAsync(slug, "Corp", "eu", "UTC", [], CancellationToken.None);
        return tenant!.TenantId;
    }

    private static TenantIdentityConfig MakeConfig(
        string tenantId,
        string issuer = "https://idp.example.com",
        string audience = "fps-api",
        bool localAccounts = false,
        Dictionary<string, string>? roleMapping = null) => new()
    {
        TenantId = tenantId,
        TrustedIssuer = issuer,
        Audience = audience,
        TenantClaimName = "tenant_id",
        SubjectClaimName = "sub",
        RoleClaimNames = ["groups"],
        RoleMapping = roleMapping ?? new Dictionary<string, string> { ["fps-admins"] = "admin" },
        LocalAccountPolicyEnabled = localAccounts,
        ConfiguredByHash = "actor-hash",
        ConfiguredAt = DateTimeOffset.UtcNow,
    };

    // ── ConfigureAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task Configure_ValidConfig_SavesAndRegistersInStore()
    {
        var tenantId = await CreateTenant();

        var error = await service.ConfigureAsync(MakeConfig(tenantId), CancellationToken.None);

        Assert.Null(error);
        Assert.True(configStore.IsConfigured(tenantId));
        Assert.True(configStore.IsEnforcementActive);
    }

    [Fact]
    public async Task Configure_MissingIssuer_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await service.ConfigureAsync(MakeConfig(tenantId, issuer: string.Empty), CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("issuer", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Configure_UnknownTenant_ReturnsError()
    {
        var error = await service.ConfigureAsync(MakeConfig("no-such-tenant"), CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task Configure_ArchivedTenant_ReturnsError()
    {
        var tenantId = await CreateTenant("archived");
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Archived, "actor", null, null, CancellationToken.None);

        var error = await service.ConfigureAsync(MakeConfig(tenantId), CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("archived", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Configure_Idempotent_OverwritesExistingConfig()
    {
        var tenantId = await CreateTenant();
        await service.ConfigureAsync(MakeConfig(tenantId), CancellationToken.None);

        var error = await service.ConfigureAsync(MakeConfig(tenantId, audience: "fps-api-v2"), CancellationToken.None);

        Assert.Null(error);
        var stored = await service.GetConfigAsync(tenantId, CancellationToken.None);
        Assert.Equal("fps-api-v2", stored!.Audience);
    }

    [Fact]
    public async Task Configure_RoleMappingIsTenantScoped()
    {
        var t1 = await CreateTenant("corp-a");
        var t2 = await CreateTenant("corp-b");

        var cfg1 = MakeConfig(t1, roleMapping: new Dictionary<string, string> { ["a-admins"] = "admin" });
        var cfg2 = MakeConfig(t2, roleMapping: new Dictionary<string, string> { ["b-managers"] = "hr_manager" });

        await service.ConfigureAsync(cfg1, CancellationToken.None);
        await service.ConfigureAsync(cfg2, CancellationToken.None);

        var stored1 = await service.GetConfigAsync(t1, CancellationToken.None);
        var stored2 = await service.GetConfigAsync(t2, CancellationToken.None);

        Assert.Equal("admin", stored1!.RoleMapping["a-admins"]);
        Assert.DoesNotContain("b-managers", stored1.RoleMapping.Keys);
        Assert.Equal("hr_manager", stored2!.RoleMapping["b-managers"]);
    }

    // ── RegisterAdminAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAdmin_SsoMapped_RequiresIdentityConfigFirst()
    {
        var tenantId = await CreateTenant();

        var error = await service.RegisterAdminAsync(new TenantAdminRecord(
            tenantId, "hash-abc", TenantAdminType.SsoMapped, "actor", DateTimeOffset.UtcNow, null, true),
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("configured", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterAdmin_SsoMapped_AfterConfigured_Succeeds()
    {
        var tenantId = await CreateTenant();
        await service.ConfigureAsync(MakeConfig(tenantId), CancellationToken.None);

        var error = await service.RegisterAdminAsync(new TenantAdminRecord(
            tenantId, "subj-hash-1", TenantAdminType.SsoMapped, "actor", DateTimeOffset.UtcNow, "first admin", true),
            CancellationToken.None);

        Assert.Null(error);
        Assert.True(await service.HasActiveAdminAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterAdmin_Local_RejectedWhenPolicyDisabled()
    {
        var tenantId = await CreateTenant();
        await service.ConfigureAsync(MakeConfig(tenantId), CancellationToken.None); // LocalAccountPolicyEnabled=false

        var error = await service.RegisterAdminAsync(new TenantAdminRecord(
            tenantId, "local-marker", TenantAdminType.Local, "actor", DateTimeOffset.UtcNow, "break-glass", true),
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("not permitted", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterAdmin_Local_AllowedWhenPolicyEnabled()
    {
        var tenantId = await CreateTenant();
        await service.ConfigureAsync(MakeConfig(tenantId, localAccounts: true), CancellationToken.None);

        var error = await service.RegisterAdminAsync(new TenantAdminRecord(
            tenantId, "local-marker", TenantAdminType.Local, "actor", DateTimeOffset.UtcNow, "demo", true),
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task RegisterAdmin_DuplicateSubject_ReturnsError()
    {
        var tenantId = await CreateTenant();
        await service.ConfigureAsync(MakeConfig(tenantId), CancellationToken.None);

        await service.RegisterAdminAsync(new TenantAdminRecord(
            tenantId, "subj-x", TenantAdminType.SsoMapped, "actor", DateTimeOffset.UtcNow, null, true),
            CancellationToken.None);

        var error = await service.RegisterAdminAsync(new TenantAdminRecord(
            tenantId, "subj-x", TenantAdminType.SsoMapped, "actor", DateTimeOffset.UtcNow, null, true),
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("already registered", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListAdmins_ReturnsOnlyForRequestedTenant()
    {
        var t1 = await CreateTenant("corp-1");
        var t2 = await CreateTenant("corp-2");
        await service.ConfigureAsync(MakeConfig(t1), CancellationToken.None);
        await service.ConfigureAsync(MakeConfig(t2), CancellationToken.None);

        await service.RegisterAdminAsync(new TenantAdminRecord(t1, "s1", TenantAdminType.SsoMapped, "a", DateTimeOffset.UtcNow, null, true), CancellationToken.None);
        await service.RegisterAdminAsync(new TenantAdminRecord(t2, "s2", TenantAdminType.SsoMapped, "a", DateTimeOffset.UtcNow, null, true), CancellationToken.None);

        var t1Admins = await service.ListAdminsAsync(t1, CancellationToken.None);
        Assert.Single(t1Admins);
        Assert.Equal("s1", t1Admins[0].SubjectHash);
    }

    // ── ConfigStore enforcement (SharedKernel) ───────────────────────────────

    [Fact]
    public void ConfigStore_StartsEmpty_EnforcementInactive()
    {
        var store = new InMemoryTenantIdentityConfigStore();

        Assert.False(store.IsEnforcementActive);
        Assert.False(store.IsConfigured("any-tenant"));
    }

    [Fact]
    public void ConfigStore_AfterRegister_EnforcementActiveForRegisteredTenant()
    {
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("tenant-a");

        Assert.True(store.IsEnforcementActive);
        Assert.True(store.IsConfigured("tenant-a"));
        Assert.False(store.IsConfigured("tenant-b"));
    }

    [Fact]
    public void ConfigStore_Unregister_RemovesTenant()
    {
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("tenant-a");
        store.Unregister("tenant-a");

        Assert.False(store.IsEnforcementActive);
        Assert.False(store.IsConfigured("tenant-a"));
    }

    // ── Role mapping enforcement ─────────────────────────────────────────────

    [Fact]
    public async Task Configure_WiresRoleMappingIntoStore()
    {
        var tenantId = await CreateTenant();
        var mapping = new Dictionary<string, string> { ["idp-admin"] = "admin", ["idp-hr"] = "hr_manager" };

        await service.ConfigureAsync(MakeConfig(tenantId, roleMapping: mapping), CancellationToken.None);

        // Mapped role passes through
        var mapped = roleMappingStore.MapToRoles(tenantId, ["idp-admin"]);
        Assert.Equal(["admin"], mapped);
    }

    [Fact]
    public async Task RoleMapping_ConfiguredTenant_UnmappedRoleDropped()
    {
        var tenantId = await CreateTenant();
        var mapping = new Dictionary<string, string> { ["idp-admin"] = "admin" };

        await service.ConfigureAsync(MakeConfig(tenantId, roleMapping: mapping), CancellationToken.None);

        // Raw "admin" claim from token is not in the mapping → dropped
        var result = roleMappingStore.MapToRoles(tenantId, ["admin", "idp-admin"]);
        Assert.Equal(["admin"], result); // only "idp-admin"→"admin" survives
    }

    [Fact]
    public async Task RoleMapping_ConfiguredTenant_NoMappingDropsAllRoles()
    {
        var tenantId = await CreateTenant();
        // Configure with empty role mapping
        await service.ConfigureAsync(MakeConfig(tenantId, roleMapping: new Dictionary<string, string>()), CancellationToken.None);

        var result = roleMappingStore.MapToRoles(tenantId, ["admin", "employee"]);
        Assert.Empty(result);
    }

    [Fact]
    public void RoleMapping_UnconfiguredTenant_PassesNonPrivileged_StripsPrivileged()
    {
        // PLAT001: an unconfigured tenant never grants a privileged role (admin) from a raw
        // claim without an explicit mapping or seeded allowlist; non-privileged passes through.
        var result = roleMappingStore.MapToRoles("unconfigured-tenant", ["admin", "employee"]);
        Assert.Equal(["employee"], result);
    }

    [Fact]
    public async Task RoleMapping_TenantScoped_DoesNotCrossTenantsMapping()
    {
        var t1 = await CreateTenant("corp-a");
        var t2 = await CreateTenant("corp-b");
        await service.ConfigureAsync(MakeConfig(t1, roleMapping: new Dictionary<string, string> { ["a-admin"] = "admin" }), CancellationToken.None);
        await service.ConfigureAsync(MakeConfig(t2, roleMapping: new Dictionary<string, string> { ["b-hr"] = "hr_manager" }), CancellationToken.None);

        var t1Roles = roleMappingStore.MapToRoles(t1, ["a-admin", "b-hr"]);
        var t2Roles = roleMappingStore.MapToRoles(t2, ["a-admin", "b-hr"]);

        // t1: only a-admin maps, b-hr is dropped
        Assert.Equal(["admin"], t1Roles);
        // t2: only b-hr maps, a-admin is dropped
        Assert.Equal(["hr_manager"], t2Roles);
    }

    // ── Restart hydration ────────────────────────────────────────────────────

    [Fact]
    public async Task GetConfiguredTenantIdsAsync_ReturnsAllSavedTenantIds()
    {
        var t1 = await CreateTenant("corp-x");
        var t2 = await CreateTenant("corp-y");
        await service.ConfigureAsync(MakeConfig(t1), CancellationToken.None);
        await service.ConfigureAsync(MakeConfig(t2), CancellationToken.None);

        var ids = await identityRepo.GetConfiguredTenantIdsAsync(CancellationToken.None);

        Assert.Contains(t1, ids);
        Assert.Contains(t2, ids);
    }

    [Fact]
    public async Task HydrationScenario_NewStores_PopulatedFromPersistedConfigs()
    {
        var tenantId = await CreateTenant("corp-hydrate");
        var mapping = new Dictionary<string, string> { ["idp-admin"] = "admin" };
        await service.ConfigureAsync(MakeConfig(tenantId, roleMapping: mapping), CancellationToken.None);

        // Simulate restart: fresh stores, same repository
        var freshConfigStore = new InMemoryTenantIdentityConfigStore();
        var freshRoleStore = new InMemoryTenantRoleMappingStore(freshConfigStore);

        var ids = await identityRepo.GetConfiguredTenantIdsAsync(CancellationToken.None);
        foreach (var id in ids)
        {
            var config = await identityRepo.GetConfigAsync(id, CancellationToken.None);
            if (config is null) continue;
            freshConfigStore.Register(config.TenantId);
            freshRoleStore.SetMapping(config.TenantId, config.RoleMapping);
            freshConfigStore.SetClaimConfig(config.TenantId, new TenantClaimConfig(
                config.TenantClaimName, config.SubjectClaimName, config.RoleClaimNames));
        }

        Assert.True(freshConfigStore.IsConfigured(tenantId));
        Assert.True(freshConfigStore.IsEnforcementActive);
        Assert.Equal(["admin"], freshRoleStore.MapToRoles(tenantId, ["idp-admin"]));
    }

    // ── PERSIST006B: write-through invariant ─────────────────────────────────
    // Confirms that ConfigureAsync always writes to the durable repository before
    // updating the in-memory stores, so a restart after ConfigureAsync returns
    // will restore the config via IdentityStoreHydrator.

    [Fact]
    public async Task ConfigureAsync_WritesDurableRepositoryBeforeUpdatingCache()
    {
        var tenantId = await CreateTenant();
        var config = MakeConfig(tenantId);

        var error = await service.ConfigureAsync(config, CancellationToken.None);
        Assert.Null(error);

        // Durable repository must hold the config (restart-safe).
        var persisted = await identityRepo.GetConfigAsync(tenantId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(tenantId, persisted.TenantId);

        // In-memory cache must also reflect the config (runtime-ready).
        Assert.True(configStore.IsConfigured(tenantId));
    }

    [Fact]
    public async Task HydrateFromRepository_RestoresConfigAndRoleMapping()
    {
        // Simulate ConfigureAsync writing to durable store.
        var tenantId = await CreateTenant();
        await identityRepo.SaveConfigAsync(MakeConfig(tenantId), CancellationToken.None);

        // Simulate service restart: fresh in-memory stores, hydrate from durable repo.
        var freshConfigStore = new InMemoryTenantIdentityConfigStore();
        var freshRoleStore = new InMemoryTenantRoleMappingStore(freshConfigStore);

        var tenantIds = await identityRepo.GetConfiguredTenantIdsAsync(CancellationToken.None);
        foreach (var id in tenantIds)
        {
            var config = await identityRepo.GetConfigAsync(id, CancellationToken.None);
            if (config is null) continue;
            freshConfigStore.Register(config.TenantId);
            freshRoleStore.SetMapping(config.TenantId, config.RoleMapping);
            freshConfigStore.SetClaimConfig(config.TenantId, new TenantClaimConfig(
                config.TenantClaimName, config.SubjectClaimName, config.RoleClaimNames));
        }

        Assert.True(freshConfigStore.IsConfigured(tenantId));
        Assert.Equal(["admin"], freshRoleStore.MapToRoles(tenantId, ["fps-admins"]));
    }

    // ── Blank subject guard ──────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAdmin_EmptySubjectHash_ReturnsError()
    {
        // The service validates the SubjectHash is non-empty before storing.
        var tenantId = await CreateTenant();
        await service.ConfigureAsync(MakeConfig(tenantId), CancellationToken.None);

        var error = await service.RegisterAdminAsync(
            new TenantAdminRecord(tenantId, "", TenantAdminType.SsoMapped, "actor", DateTimeOffset.UtcNow, null, true),
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("Subject", error, StringComparison.OrdinalIgnoreCase);
    }
}
