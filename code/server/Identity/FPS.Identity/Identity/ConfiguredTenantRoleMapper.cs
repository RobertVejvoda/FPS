using Microsoft.Extensions.Configuration;

namespace FPS.Identity.Identity;

public sealed class ConfiguredTenantRoleMapper(IConfiguration configuration) : ITenantRoleMapper
{
    public IReadOnlyList<string> MapToRoles(string tenantId, IEnumerable<string> incomingRoles)
    {
        var section = configuration.GetSection($"TenantRoleMapping:{tenantId}");

        if (!section.Exists())
            return incomingRoles.ToList();

        // When mapping is configured for a tenant, only explicitly mapped roles are included.
        // Unmapped roles are ignored per the SSO-first integration contract.
        var mapped = new List<string>();
        foreach (var role in incomingRoles)
        {
            var target = section[role];
            if (target is not null)
                mapped.Add(target);
        }
        return mapped;
    }
}
