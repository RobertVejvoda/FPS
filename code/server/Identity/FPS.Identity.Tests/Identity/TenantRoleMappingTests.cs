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

    private static TenantClaimsTransformation TransformationWithMapping(Dictionary<string, string?> mapping)
    {
        var mapper = MapperWithConfig(mapping);
        var store = new InMemoryDeactivatedUserStore();
        return new TenantClaimsTransformation(mapper, store);
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
        var transform = new TenantClaimsTransformation(mapper, store);

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
}
