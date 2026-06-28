using Microsoft.Extensions.Configuration;

namespace FPS.SharedKernel.Identity;

public sealed class ConfiguredTenantRoleMapper(IConfiguration configuration) : ITenantRoleMapper
{
    public IReadOnlyList<string> MapToRoles(string tenantId, IEnumerable<string> incomingRoles)
    {
        var section = configuration.GetSection($"TenantRoleMapping:{tenantId}");

        // PLAT001: a tenant's IdP can never yield a cross-tenant platform role.
        // Strip any platform_* role from both the passthrough and the mapped output,
        // so a misconfigured mapping or a customer IdP claim cannot escalate to the
        // platform plane. (TenantClaimsTransformation also strips platform roles from
        // customer-issuer tokens — this is the defence-in-depth pair.)
        if (!section.Exists())
            return incomingRoles.Where(r => !FpsRoles.IsPlatformRole(r)).ToList();

        // When mapping is configured for a tenant, only explicitly mapped roles are included.
        // Unmapped groups are ignored per the SSO-first integration contract.
        var mapped = new List<string>();
        foreach (var role in incomingRoles)
        {
            var target = section[role];
            if (target is not null && !FpsRoles.IsPlatformRole(target))
                mapped.Add(target);
        }
        return mapped;
    }
}
