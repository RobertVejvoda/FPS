using Microsoft.Extensions.Configuration;

namespace FPS.SharedKernel.Identity;

public sealed class ConfiguredTenantRoleMapper(IConfiguration configuration) : ITenantRoleMapper
{
    public IReadOnlyList<string> MapToRoles(string tenantId, IEnumerable<string> incomingRoles)
    {
        var section = configuration.GetSection($"TenantRoleMapping:{tenantId}");

        // PLAT001: a tenant with no explicit mapping never grants privileged or platform
        // roles implicitly from a raw token claim. It yields only non-privileged roles
        // (e.g. employee), unless the deployment seeds an allowlist of trusted realm roles
        // (Auth:TrustedRealmRoles) — used by the FairSpot-controlled single-realm profile
        // so its admin/hr_manager/... realm roles are honored. platform_* is always stripped
        // (TenantClaimsTransformation strips it from customer tokens too — defence in depth).
        if (!section.Exists())
        {
            var allow = TrustedRealmRoles();
            return incomingRoles
                .Where(r => !FpsRoles.IsPlatformRole(r)
                            && (!FpsRoles.IsPrivileged(r) || allow.Contains(r, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }

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

    private IReadOnlyList<string> TrustedRealmRoles()
    {
        var raw = configuration["Auth:TrustedRealmRoles"];
        return string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
