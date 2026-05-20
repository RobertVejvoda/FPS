using FPS.Identity.Identity;
using Microsoft.Extensions.Configuration;

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
}
