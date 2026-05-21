using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace FPS.SharedKernel.Identity;

// Runs on every authenticated request (before authorization) to:
// 1. Extract tenant/subject using configured claim names for configured tenants.
// 2. Replace raw IdP role claims with tenant-mapped FPS role names.
// 3. Add fps_deactivated=true for unconfigured tenants (when enforcement active),
//    missing required claims (when enforcement active), or deactivated users.
//
// FAIL-CLOSED CONTRACT:
// When enforcement is active (any tenant registered) OR per-tenant claim config exists,
// a principal with a missing/empty tenant or subject claim has its role claims stripped
// and fps_deactivated=true added. It is never returned with raw role claims intact.
// When enforcement is inactive (store empty) and no claim config exists, missing claims
// result in the original principal being returned unchanged (backward-compatible).
//
// IClaimsTransformation may be invoked more than once; fps_transformed=true prevents
// double-processing.
public sealed class TenantClaimsTransformation(
    ITenantRoleMapper roleMapper,
    IDeactivatedUserStore deactivatedUsers,
    ITenantIdentityConfigStore identityConfigStore) : IClaimsTransformation
{
    internal const string DeactivatedClaim = "fps_deactivated";
    private const string TransformedClaim = "fps_transformed";
    private const string DefaultTenantClaim = "tenant_id";
    private const string DefaultSubjectClaim = "sub";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(TransformedClaim, "true"))
            return Task.FromResult(principal);

        var enforcement = identityConfigStore.IsEnforcementActive;

        // Step 1: extract tenant from default claim.
        var tenantId = principal.FindFirstValue(DefaultTenantClaim) ?? string.Empty;
        if (string.IsNullOrEmpty(tenantId))
            return enforcement ? FailClosed(principal) : Task.FromResult(principal);

        // Step 2: per-tenant claim config (populated by Customer service on configure).
        var claimConfig = (identityConfigStore as InMemoryTenantIdentityConfigStore)
            ?.GetClaimConfig(tenantId);

        // Step 3: re-derive tenantId from the configured TenantClaimName if it differs.
        if (claimConfig is not null &&
            !string.Equals(claimConfig.TenantClaimName, DefaultTenantClaim, StringComparison.OrdinalIgnoreCase))
        {
            tenantId = principal.FindFirstValue(claimConfig.TenantClaimName) ?? string.Empty;
            if (string.IsNullOrEmpty(tenantId))
                return FailClosed(principal); // configured tenant claim absent → fail closed
        }

        // Step 4: extract stable subject using configured SubjectClaimName or defaults.
        var subjectClaimName = claimConfig?.SubjectClaimName ?? DefaultSubjectClaim;
        var userId = principal.FindFirstValue(subjectClaimName)
            ?? (string.Equals(subjectClaimName, DefaultSubjectClaim, StringComparison.OrdinalIgnoreCase)
                ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                : null)
            ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return (enforcement || claimConfig is not null)
                ? FailClosed(principal)   // required stable subject absent → fail closed
                : Task.FromResult(principal);

        // Step 5: clone and rebuild role claims.
        var cloned = principal.Clone();
        var identity = (ClaimsIdentity)cloned.Identity!;

        // Materialize before removing — LINQ over ClaimsIdentity is lazy.
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

        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
            identity.RemoveClaim(claim);
        foreach (var role in roleMapper.MapToRoles(tenantId, rawRoleValues))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

        // Step 6: enforcement and deactivation checks.
        if (enforcement && !identityConfigStore.IsConfigured(tenantId))
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

    // Clones principal, strips role claims, adds fps_deactivated+fps_transformed.
    // Ensures enforcement-active fail paths never return raw token roles.
    private static Task<ClaimsPrincipal> FailClosed(ClaimsPrincipal original)
    {
        var cloned = original.Clone();
        var identity = (ClaimsIdentity)cloned.Identity!;
        foreach (var role in identity.FindAll(ClaimTypes.Role).ToList())
            identity.RemoveClaim(role);
        identity.AddClaim(new Claim(DeactivatedClaim, "true"));
        identity.AddClaim(new Claim(TransformedClaim, "true"));
        return Task.FromResult(cloned);
    }
}
