using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
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
public sealed class TenantClaimsTransformation : IClaimsTransformation
{
    private readonly ITenantRoleMapper roleMapper;
    private readonly IDeactivatedUserStore deactivatedUsers;
    private readonly ITenantIdentityConfigStore identityConfigStore;
    private readonly IConfiguration configuration;

    public TenantClaimsTransformation(
        ITenantRoleMapper roleMapper,
        IDeactivatedUserStore deactivatedUsers,
        ITenantIdentityConfigStore identityConfigStore,
        IConfiguration configuration)
    {
        this.roleMapper = roleMapper;
        this.deactivatedUsers = deactivatedUsers;
        this.identityConfigStore = identityConfigStore;
        this.configuration = configuration;
    }

    // Back-compat overload (no platform issuer configured → platform plane dormant).
    // Used by unit tests and any caller that does not set Auth:PlatformIssuer. DI
    // resolves the greedier 4-arg constructor in services.
    public TenantClaimsTransformation(
        ITenantRoleMapper roleMapper,
        IDeactivatedUserStore deactivatedUsers,
        ITenantIdentityConfigStore identityConfigStore)
        : this(roleMapper, deactivatedUsers, identityConfigStore, new ConfigurationBuilder().Build())
    {
    }

    internal const string DeactivatedClaim = "fps_deactivated";
    internal const string PlatformClaim = "fps_platform";
    private const string TransformedClaim = "fps_transformed";
    private const string DefaultTenantClaim = "tenant_id";
    private const string DefaultSubjectClaim = "sub";
    private const string IssuerClaim = "iss";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(TransformedClaim, "true"))
            return Task.FromResult(principal);

        // PLAT001 — platform plane. A token from the trusted platform issuer carries
        // cross-tenant platform_* roles and has no customer tenant_id, so it is handled
        // before the tenant-extraction fail-closed path below (which would otherwise
        // strip its roles). When no platform issuer is configured the platform plane is
        // dormant and every token is treated as a customer-tenant token. The customer
        // path never yields platform_* roles (the role mapper strips them), so a
        // customer-issuer token can never reach the platform plane.
        // One config key drives both: Auth:PlatformAuthority activates the multi-issuer
        // JWT and (here) the role gating; Auth:PlatformIssuer overrides only if the iss
        // claim differs from the realm URL. Trailing slashes are normalized.
        var platformIssuer = (configuration["Auth:PlatformIssuer"]
            ?? configuration["Auth:PlatformAuthority"])?.TrimEnd('/');
        if (!string.IsNullOrEmpty(platformIssuer) &&
            string.Equals(principal.FindFirstValue(IssuerClaim)?.TrimEnd('/'), platformIssuer, StringComparison.Ordinal))
        {
            return TransformPlatform(principal);
        }

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

            // JwtBearer can normalize JWT "roles" into ClaimTypes.Role before this
            // transformation runs. Treat the normalized claim as the configured
            // source only when the tenant explicitly configured the JWT roles claim.
            if (roleClaimNames.Any(cn => string.Equals(cn, "roles", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(cn, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)))
            {
                rawRoleValues.AddRange(identity.FindAll(ClaimTypes.Role).Select(c => c.Value));
                rawRoleValues = rawRoleValues
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        else
        {
            rawRoleValues = identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        }

        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
            identity.RemoveClaim(claim);
        // PLAT001: a customer-issuer token never grants a platform_* role, whichever
        // ITenantRoleMapper is registered (this is the universal gate; the mapper
        // guard is defence-in-depth). Platform roles only come from the platform
        // branch above.
        foreach (var role in roleMapper.MapToRoles(tenantId, rawRoleValues))
            if (!FpsRoles.IsPlatformRole(role))
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

    // PLAT001 — platform-issuer token. Keeps only cross-tenant platform_* roles
    // (the JWT layer has already validated they came from the trusted platform
    // issuer), drops any tenant-plane role, and marks the principal fps_platform.
    // No customer tenant_id is required.
    private static Task<ClaimsPrincipal> TransformPlatform(ClaimsPrincipal original)
    {
        var cloned = original.Clone();
        var identity = (ClaimsIdentity)cloned.Identity!;
        foreach (var role in identity.FindAll(ClaimTypes.Role).ToList())
        {
            if (!FpsRoles.IsPlatformRole(role.Value))
                identity.RemoveClaim(role);
        }
        identity.AddClaim(new Claim(PlatformClaim, "true"));
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
