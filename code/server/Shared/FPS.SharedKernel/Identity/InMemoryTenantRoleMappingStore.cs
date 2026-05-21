using System.Collections.Concurrent;

namespace FPS.SharedKernel.Identity;

// Tenant-scoped role mapper backed by API-configured mappings.
// When a tenant is registered in ITenantIdentityConfigStore:
//   - uses the explicit mapping; unmapped raw claims are dropped (fail closed).
//   - if no mapping has been registered yet for the tenant, all roles are dropped.
// When a tenant is NOT in the config store (enforcement inactive):
//   - passes incoming claims through unchanged (backward-compatible).
public sealed class InMemoryTenantRoleMappingStore(ITenantIdentityConfigStore configStore) : ITenantRoleMapper
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> mappings =
        new(StringComparer.OrdinalIgnoreCase);

    public void SetMapping(string tenantId, IReadOnlyDictionary<string, string> mapping) =>
        mappings[tenantId] = mapping;

    public IReadOnlyList<string> MapToRoles(string tenantId, IEnumerable<string> incomingRoles)
    {
        if (!configStore.IsConfigured(tenantId))
            return incomingRoles.ToList();

        if (!mappings.TryGetValue(tenantId, out var mapping))
            return [];

        var result = new List<string>();
        foreach (var role in incomingRoles)
        {
            if (mapping.TryGetValue(role, out var mapped))
                result.Add(mapped);
        }
        return result;
    }
}
