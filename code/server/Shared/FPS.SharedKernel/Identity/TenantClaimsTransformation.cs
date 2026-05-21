using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace FPS.SharedKernel.Identity;

// Runs on every authenticated request (before authorization) to:
// 1. Extract tenant/subject using configured claim names for configured tenants.
// 2. Replace raw IdP role claims with tenant-mapped FPS role names.
// 3. Add fps_deactivated=true for unconfigured tenants (when enforcement active)
//    or for users in the per-service deactivation store.
//
// For configured tenants the transformation uses the stored TenantClaimName,
// SubjectClaimName, and RoleClaimNames from ITenantIdentityConfigStore. Tokens
// that do not carry the configured tenant/subject claim fail closed — no transform,
// no roles. This enforces the contract that stable subjects and correct claim names
// are required before a user is recognized.
//
// IClaimsTransformation may be invoked more than once per principal; fps_transformed=true
// guards against double-mapping.
public sealed class TenantClaimsTransformation(
    ITenantRoleMapper roleMapper,
    IDeactivatedUserStore deactivatedUsers,
    ITenantIdentityConfigStore identityConfigStore) : IClaimsTransformation
{
    internal const string DeactivatedClaim = "fps_deactivated";
    private const string TransformedClaim = "fps_transformed";

    // Default claim names used when no tenant-specific config exists.
    private const string DefaultTenantClaim = "tenant_id";
    private const string DefaultSubjectClaim = "sub";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(TransformedClaim, "true"))
            return Task.FromResult(principal);

        // Phase 1: extract tenant from the default claim (bootstrap step).
        var tenantId = principal.FindFirstValue(DefaultTenantClaim) ?? string.Empty;
        if (string.IsNullOrEmpty(tenantId))
            return Task.FromResult(principal);

        // Phase 2: resolve per-tenant claim names for configured tenants.
        var claimConfig = identityConfigStore is InMemoryTenantIdentityConfigStore concreteStore
            ? concreteStore.GetClaimConfig(tenantId)
            : null;

        // If tenant is configured and has a non-default TenantClaimName, re-read tenantId.
        if (claimConfig is not null &&
            !string.Equals(claimConfig.TenantClaimName, DefaultTenantClaim, StringComparison.OrdinalIgnoreCase))
        {
            tenantId = principal.FindFirstValue(claimConfig.TenantClaimName) ?? string.Empty;
            if (string.IsNullOrEmpty(tenantId))
                return Task.FromResult(principal); // fail closed: required claim absent
        }

        // Extract stable subject using configured SubjectClaimName, or defaults.
        var subjectClaimName = claimConfig?.SubjectClaimName ?? DefaultSubjectClaim;
        var userId = principal.FindFirstValue(subjectClaimName)
            ?? (string.Equals(subjectClaimName, DefaultSubjectClaim, StringComparison.OrdinalIgnoreCase)
                ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                : null)
            ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Task.FromResult(principal); // fail closed: stable subject absent

        var cloned = principal.Clone();
        var identity = (ClaimsIdentity)cloned.Identity!;

        // Phase 3: materialize raw roles before removing any claims (LINQ is lazy).
        List<string> rawRoleValues;
        if (claimConfig?.RoleClaimNames is { Count: > 0 } roleClaimNames)
        {
            rawRoleValues = roleClaimNames
                .SelectMany(cn => identity.FindAll(cn))
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            rawRoleValues = identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        }

        // Remove all existing role claims before replacing with mapped values.
        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
            identity.RemoveClaim(claim);
        foreach (var role in roleMapper.MapToRoles(tenantId, rawRoleValues))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

        // Reject unconfigured tenants when enforcement is active.
        if (identityConfigStore.IsEnforcementActive && !identityConfigStore.IsConfigured(tenantId))
        {
            foreach (var role in identity.FindAll(ClaimTypes.Role).ToList())
                identity.RemoveClaim(role);
            identity.AddClaim(new Claim(DeactivatedClaim, "true"));
        }
        else if (deactivatedUsers.IsDeactivated(tenantId, userId))
        {
            foreach (var role in identity.FindAll(ClaimTypes.Role).ToList())
                identity.RemoveClaim(role);
            identity.AddClaim(new Claim(DeactivatedClaim, "true"));
        }

        identity.AddClaim(new Claim(TransformedClaim, "true"));
        return Task.FromResult(cloned);
    }
}
