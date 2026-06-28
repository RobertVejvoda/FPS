using FPS.SharedKernel.Identity;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace FPS.Identity.Tests.Identity;

public sealed class TenantRoleMappingTests
{
    private static ITenantRoleMapper MapperWithConfig(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new ConfiguredTenantRoleMapper(config);
    }

    private static TenantClaimsTransformation TransformationWithMapping(
        Dictionary<string, string?> mapping,
        InMemoryTenantIdentityConfigStore? configStore = null)
    {
        var mapper = MapperWithConfig(mapping);
        var store = new InMemoryDeactivatedUserStore();
        return new TenantClaimsTransformation(mapper, store, configStore ?? new InMemoryTenantIdentityConfigStore());
    }

    private static ClaimsPrincipal PrincipalWithRole(string tenantId, string userId, string role)
    {
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("tenant_id", tenantId),
            new Claim(ClaimTypes.Role, role),
        ], "test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void MapToRoles_NoMappingConfigured_PassesThroughRoles()
    {
        var mapper = MapperWithConfig([]);

        var roles = mapper.MapToRoles("tenant-1", ["employee", "hr_manager"]);

        Assert.Contains("employee", roles);
        Assert.Contains("hr_manager", roles);
    }

    [Fact]
    public void MapToRoles_WithMapping_TranslatesConfiguredRoles()
    {
        var mapper = MapperWithConfig(new Dictionary<string, string?>
        {
            ["TenantRoleMapping:tenant-1:hr_group"] = "HrAdmin",
            ["TenantRoleMapping:tenant-1:employees"] = "Employee",
        });

        var roles = mapper.MapToRoles("tenant-1", ["hr_group", "employees"]);

        Assert.Contains("HrAdmin", roles);
        Assert.Contains("Employee", roles);
    }

    [Fact]
    public void MapToRoles_WithMapping_UnmappedRolesAreIgnored()
    {
        var mapper = MapperWithConfig(new Dictionary<string, string?>
        {
            ["TenantRoleMapping:tenant-1:employees"] = "Employee",
        });

        var roles = mapper.MapToRoles("tenant-1", ["employees", "unknown_group"]);

        Assert.Contains("Employee", roles);
        Assert.DoesNotContain("unknown_group", roles);
    }

    [Fact]
    public void MapToRoles_MappingForDifferentTenant_DoesNotApply()
    {
        var mapper = MapperWithConfig(new Dictionary<string, string?>
        {
            ["TenantRoleMapping:tenant-2:employees"] = "Employee",
        });

        // tenant-1 has no mapping → pass through
        var roles = mapper.MapToRoles("tenant-1", ["employees"]);

        Assert.Contains("employees", roles);
    }

    [Fact]
    public void MapToRoles_EmptyIncomingRoles_ReturnsEmpty()
    {
        var mapper = MapperWithConfig([]);

        var roles = mapper.MapToRoles("tenant-1", []);

        Assert.Empty(roles);
    }

    // Idempotency tests for TenantClaimsTransformation

    [Fact]
    public async Task Transform_CalledTwice_MappedRoleStillPresent()
    {
        // Regression: second call must not re-process already-mapped roles.
        // idp_hr_group → HrAdmin on first call. Second call must see HrAdmin intact,
        // not treat HrAdmin as an unmapped IdP group and drop it.
        var transform = TransformationWithMapping(new Dictionary<string, string?>
        {
            ["TenantRoleMapping:tenant-1:idp_hr_group"] = "HrAdmin",
        });

        var principal = PrincipalWithRole("tenant-1", "user-1", "idp_hr_group");

        var once = await transform.TransformAsync(principal);
        var twice = await transform.TransformAsync(once);

        var roles = twice.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("HrAdmin", roles);
        Assert.DoesNotContain("idp_hr_group", roles);
    }

    [Fact]
    public async Task Transform_CalledTwice_DeactivatedUserRemainsDeactivated()
    {
        var mapper = MapperWithConfig([]);
        var store = new InMemoryDeactivatedUserStore();
        var transform = new TenantClaimsTransformation(mapper, store, new InMemoryTenantIdentityConfigStore());

        var principal = PrincipalWithRole("tenant-1", "user-deactivated", "employee");
        store.Deactivate("tenant-1", "user-deactivated");

        var once = await transform.TransformAsync(principal);
        var twice = await transform.TransformAsync(once);

        Assert.True(twice.HasClaim("fps_deactivated", "true"));
        Assert.Empty(twice.FindAll(ClaimTypes.Role).ToList());
    }

    [Fact]
    public async Task Transform_CalledTwice_RoleCountUnchanged()
    {
        var transform = TransformationWithMapping(new Dictionary<string, string?>
        {
            ["TenantRoleMapping:tenant-1:idp_role"] = "FpsRole",
        });

        var principal = PrincipalWithRole("tenant-1", "user-1", "idp_role");

        var once = await transform.TransformAsync(principal);
        var twice = await transform.TransformAsync(once);

        // Exactly one role claim — not doubled or lost
        Assert.Single(twice.FindAll(ClaimTypes.Role));
        Assert.True(twice.HasClaim(ClaimTypes.Role, "FpsRole"));
    }

    // Identity config store enforcement

    [Fact]
    public async Task Transform_EmptyConfigStore_AllowsAnyTenant()
    {
        // Before any tenant is configured, enforcement is inactive.
        var store = new InMemoryTenantIdentityConfigStore();
        var transform = TransformationWithMapping([], store);

        var principal = PrincipalWithRole("tenant-unknown", "user-1", "employee");
        var result = await transform.TransformAsync(principal);

        Assert.False(result.HasClaim("fps_deactivated", "true"));
    }

    [Fact]
    public async Task Transform_ConfiguredTenant_Allowed()
    {
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("tenant-1");
        var transform = TransformationWithMapping([], store);

        var principal = PrincipalWithRole("tenant-1", "user-1", "employee");
        var result = await transform.TransformAsync(principal);

        Assert.False(result.HasClaim("fps_deactivated", "true"));
    }

    [Fact]
    public async Task Transform_UnconfiguredTenant_AfterEnforcementActive_IsRejected()
    {
        // Once any tenant is registered, users from unconfigured tenants are deactivated.
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("tenant-1");   // enforcement now active
        var transform = TransformationWithMapping([], store);

        var principal = PrincipalWithRole("tenant-unknown", "user-x", "employee");
        var result = await transform.TransformAsync(principal);

        Assert.True(result.HasClaim("fps_deactivated", "true"));
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    // Per-tenant claim name enforcement

    private static ClaimsPrincipal PrincipalWithClaims(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.type, c.value)), "test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task Transform_ConfiguredTenant_UsesStoredRoleClaimName()
    {
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("tenant-1");
        store.SetClaimConfig("tenant-1", new TenantClaimConfig("tenant_id", "sub", ["groups"]));

        var roleMappingStore = new InMemoryTenantRoleMappingStore(store);
        roleMappingStore.SetMapping("tenant-1", new Dictionary<string, string> { ["fp-admins"] = "admin" });

        var transform = new TenantClaimsTransformation(roleMappingStore, new InMemoryDeactivatedUserStore(), store);

        // Token has "groups" claim, not ClaimTypes.Role
        var principal = PrincipalWithClaims(
            ("tenant_id", "tenant-1"), ("sub", "user-1"), ("groups", "fp-admins"));

        var result = await transform.TransformAsync(principal);

        Assert.True(result.HasClaim(ClaimTypes.Role, "admin"));
        Assert.False(result.HasClaim("fps_deactivated", "true"));
    }

    [Fact]
    public async Task Transform_ConfiguredTenant_UsesNormalizedRolesClaimWhenRolesConfigured()
    {
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("tenant-1");
        store.SetClaimConfig("tenant-1", new TenantClaimConfig("tenant_id", "sub", ["roles"]));

        var roleMappingStore = new InMemoryTenantRoleMappingStore(store);
        roleMappingStore.SetMapping("tenant-1", new Dictionary<string, string> { ["admin"] = "admin" });

        var transform = new TenantClaimsTransformation(roleMappingStore, new InMemoryDeactivatedUserStore(), store);

        // JwtBearer can normalize JWT "roles" into ClaimTypes.Role before transformation.
        var principal = PrincipalWithClaims(
            ("tenant_id", "tenant-1"), ("sub", "user-1"), (ClaimTypes.Role, "admin"));

        var result = await transform.TransformAsync(principal);

        Assert.True(result.HasClaim(ClaimTypes.Role, "admin"));
        Assert.False(result.HasClaim("fps_deactivated", "true"));
    }

    [Fact]
    public async Task Transform_ConfiguredTenant_UsesStoredSubjectClaimName()
    {
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("tenant-1");
        store.SetClaimConfig("tenant-1", new TenantClaimConfig("tenant_id", "oid", []));

        var transform = new TenantClaimsTransformation(
            new InMemoryTenantRoleMappingStore(store), new InMemoryDeactivatedUserStore(), store);

        // Token has "oid" as stable subject — transform should proceed
        var principal = PrincipalWithClaims(
            ("tenant_id", "tenant-1"), ("oid", "stable-object-id"));

        var result = await transform.TransformAsync(principal);

        Assert.False(result.HasClaim("fps_deactivated", "true"));
    }

    [Fact]
    public async Task Transform_ConfiguredTenant_MissingConfiguredSubjectClaim_StripsRolesAndDeactivates()
    {
        // Enforcement active; "oid" required but token only has "sub" — raw admin must not survive.
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("tenant-1");
        store.SetClaimConfig("tenant-1", new TenantClaimConfig("tenant_id", "oid", []));

        var transform = new TenantClaimsTransformation(
            new InMemoryTenantRoleMappingStore(store), new InMemoryDeactivatedUserStore(), store);

        var principal = PrincipalWithClaims(
            ("tenant_id", "tenant-1"), ("sub", "user-1"),
            (ClaimTypes.Role, "admin")); // raw admin claim — must be stripped

        var result = await transform.TransformAsync(principal);

        Assert.True(result.HasClaim("fps_deactivated", "true"));
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Transform_ConfiguredTenant_MissingTenantClaim_StripsRolesAndDeactivates()
    {
        // Token carries default "tenant_id" but config requires "tid" — raw admin must not survive.
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("tenant-1");
        store.SetClaimConfig("tenant-1", new TenantClaimConfig("tid", "sub", []));

        var transform = new TenantClaimsTransformation(
            new InMemoryTenantRoleMappingStore(store), new InMemoryDeactivatedUserStore(), store);

        var principal = PrincipalWithClaims(
            ("tenant_id", "tenant-1"), ("sub", "user-1"),
            (ClaimTypes.Role, "admin")); // raw admin claim — must be stripped

        var result = await transform.TransformAsync(principal);

        Assert.True(result.HasClaim("fps_deactivated", "true"));
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Transform_EnforcementActive_MissingDefaultTenantClaim_StripsRoles()
    {
        // Enforcement active; token has no tenant_id at all — raw roles must not survive.
        var store = new InMemoryTenantIdentityConfigStore();
        store.Register("some-tenant"); // enforcement now active

        var transform = new TenantClaimsTransformation(
            new InMemoryTenantRoleMappingStore(store), new InMemoryDeactivatedUserStore(), store);

        var principal = PrincipalWithClaims(("sub", "user-1"), (ClaimTypes.Role, "admin"));

        var result = await transform.TransformAsync(principal);

        Assert.True(result.HasClaim("fps_deactivated", "true"));
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Transform_EnforcementInactive_MissingTenantClaim_ReturnsOriginalUnchanged()
    {
        // Before any tenant configured (enforcement inactive) — backward-compatible pass-through.
        var store = new InMemoryTenantIdentityConfigStore(); // empty

        var transform = new TenantClaimsTransformation(
            new InMemoryTenantRoleMappingStore(store), new InMemoryDeactivatedUserStore(), store);

        var principal = PrincipalWithClaims(("sub", "user-1"), (ClaimTypes.Role, "admin"));

        var result = await transform.TransformAsync(principal);

        Assert.False(result.HasClaim("fps_deactivated", "true"));
    }

    // ── PLAT001: platform-plane gating ─────────────────────────────────────────

    private const string PlatformIssuer = "https://platform.example/realms/fps-platform";
    private const string CustomerIssuer = "https://customer.example/realms/fairspot";

    private static TenantClaimsTransformation TransformationWithPlatformIssuer(string platformIssuer) =>
        new(MapperWithConfig([]), new InMemoryDeactivatedUserStore(), new InMemoryTenantIdentityConfigStore(),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:PlatformIssuer"] = platformIssuer })
                .Build());

    private static ClaimsPrincipal PrincipalWithIssuer(string issuer, string? tenantId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "op-1"), new("iss", issuer) };
        if (tenantId is not null) claims.Add(new Claim("tenant_id", tenantId));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task PlatformIssuerToken_KeepsPlatformRole_WithoutTenantId()
    {
        var transform = TransformationWithPlatformIssuer(PlatformIssuer);

        var result = await transform.TransformAsync(
            PrincipalWithIssuer(PlatformIssuer, tenantId: null, FpsRoles.PlatformAdmin));

        Assert.True(result.IsInRole(FpsRoles.PlatformAdmin));
        Assert.True(result.HasClaim("fps_platform", "true"));
    }

    [Fact]
    public async Task PlatformIssuerToken_DropsTenantPlaneRoles()
    {
        var transform = TransformationWithPlatformIssuer(PlatformIssuer);

        var result = await transform.TransformAsync(
            PrincipalWithIssuer(PlatformIssuer, tenantId: null, FpsRoles.PlatformAdmin, FpsRoles.Admin));

        Assert.True(result.IsInRole(FpsRoles.PlatformAdmin));
        Assert.False(result.IsInRole(FpsRoles.Admin)); // tenant-plane role dropped on a platform token
    }

    [Fact]
    public async Task CustomerIssuerToken_WithPlatformRoleClaim_IsStripped()
    {
        var transform = TransformationWithPlatformIssuer(PlatformIssuer);

        // A customer-issuer token must never reach the platform plane, even if its
        // IdP injects a platform_admin role claim.
        var result = await transform.TransformAsync(
            PrincipalWithIssuer(CustomerIssuer, tenantId: "acme", FpsRoles.PlatformAdmin));

        Assert.False(result.IsInRole(FpsRoles.PlatformAdmin));
        Assert.False(result.HasClaim("fps_platform", "true"));
    }

    [Fact]
    public void Mapper_StripsPlatformRole_FromPassthrough()
    {
        var mapper = MapperWithConfig([]);

        var roles = mapper.MapToRoles("acme", ["employee", FpsRoles.PlatformAdmin]);

        Assert.Contains("employee", roles);
        Assert.DoesNotContain(FpsRoles.PlatformAdmin, roles);
    }

    [Fact]
    public void Mapper_RefusesToMapTenantGroup_ToPlatformRole()
    {
        var mapper = MapperWithConfig(new Dictionary<string, string?>
        {
            ["TenantRoleMapping:acme:ops_group"] = FpsRoles.PlatformAdmin, // misconfiguration attempt
            ["TenantRoleMapping:acme:staff"] = "employee",
        });

        var roles = mapper.MapToRoles("acme", ["ops_group", "staff"]);

        Assert.DoesNotContain(FpsRoles.PlatformAdmin, roles);
        Assert.Contains("employee", roles);
    }
}
