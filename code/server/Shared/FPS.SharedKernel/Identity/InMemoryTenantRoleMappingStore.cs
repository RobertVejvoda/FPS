using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace FPS.SharedKernel.Identity;

// Tenant-scoped role mapper backed by API-configured mappings.
// When a tenant is registered in ITenantIdentityConfigStore:
//   - uses the explicit mapping; unmapped raw claims are dropped (fail closed).
//   - if no mapping has been registered yet for the tenant, all roles are dropped.
// When a tenant is NOT in the config store (enforcement inactive):
//   - passes non-privileged claims through. PLAT001: privileged (admin/hr_manager/...) and
//     platform_* roles are never granted to an unconfigured tenant from a raw claim — they
//     require explicit per-tenant configuration, or a seeded Auth:TrustedRealmRoles allowlist
//     (the FairSpot-controlled single-realm profile) which lists the realm roles that may
//     pass through.
public sealed class InMemoryTenantRoleMappingStore : ITenantRoleMapper
{
    private readonly ITenantIdentityConfigStore configStore;
    private readonly IConfiguration configuration;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> mappings =
        new(StringComparer.OrdinalIgnoreCase);

    public InMemoryTenantRoleMappingStore(ITenantIdentityConfigStore configStore, IConfiguration configuration)
    {
        this.configStore = configStore;
        this.configuration = configuration;
    }

    // Back-compat (empty allowlist): privileged roles never pass through for an unconfigured tenant.
    public InMemoryTenantRoleMappingStore(ITenantIdentityConfigStore configStore)
        : this(configStore, new ConfigurationBuilder().Build())
    {
    }

    public void SetMapping(string tenantId, IReadOnlyDictionary<string, string> mapping) =>
        mappings[tenantId] = mapping;

    public IReadOnlyList<string> MapToRoles(string tenantId, IEnumerable<string> incomingRoles)
    {
        if (!configStore.IsConfigured(tenantId))
        {
            // Read the allowlist per-call so test/host configuration added after construction
            // is honored (matches ConfiguredTenantRoleMapper).
            var raw = configuration["Auth:TrustedRealmRoles"];
            var allow = string.IsNullOrWhiteSpace(raw)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(
                    raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);
            return incomingRoles
                .Where(r => !FpsRoles.IsPlatformRole(r) && (!FpsRoles.IsPrivileged(r) || allow.Contains(r)))
                .ToList();
        }

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
