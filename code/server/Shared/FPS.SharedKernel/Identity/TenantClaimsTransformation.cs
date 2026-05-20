using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace FPS.SharedKernel.Identity;

// Runs on every authenticated request (before authorization) to:
// 1. Replace raw IdP role claims with tenant-mapped FPS role names.
// 2. Add fps_deactivated=true when the user is in the per-service deactivation store.
//
// The fps_deactivated claim is checked by the default authorization policy registered
// via AddFpsAuthorization(), so all [Authorize]-protected endpoints fail closed for
// deactivated users without requiring changes to individual controllers.
//
// IClaimsTransformation may be invoked more than once for a principal. The transformation
// uses fps_transformed=true as a marker to skip re-mapping on subsequent calls, keeping
// the result stable regardless of invocation count.
//
// Note: IDeactivatedUserStore is in-memory per service. For cross-service deactivation
// enforcement, the primary mechanism is Keycloak user deactivation (which prevents token
// issuance). The in-memory store covers fast-path denial within a single service instance.
public sealed class TenantClaimsTransformation(
    ITenantRoleMapper roleMapper,
    IDeactivatedUserStore deactivatedUsers) : IClaimsTransformation
{
    internal const string DeactivatedClaim = "fps_deactivated";
    private const string TransformedClaim = "fps_transformed";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Skip if already transformed — IClaimsTransformation can be called more than once.
        if (principal.HasClaim(TransformedClaim, "true"))
            return Task.FromResult(principal);

        var tenantId = principal.FindFirstValue("tenant_id") ?? string.Empty;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? string.Empty;

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId))
            return Task.FromResult(principal);

        var cloned = principal.Clone();
        var identity = (ClaimsIdentity)cloned.Identity!;

        // Replace raw IdP role claims with tenant-mapped FPS role names
        var rawRoles = identity.FindAll(ClaimTypes.Role).ToList();
        foreach (var claim in rawRoles)
            identity.RemoveClaim(claim);
        foreach (var role in roleMapper.MapToRoles(tenantId, rawRoles.Select(c => c.Value)))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

        // Deactivated users: strip all role claims so [Authorize(Roles = "...")] fails,
        // and add a marker claim so the DefaultPolicy assertion also rejects them.
        // This covers both role-based and policy-based authorization without changing controllers.
        if (deactivatedUsers.IsDeactivated(tenantId, userId))
        {
            foreach (var role in identity.FindAll(ClaimTypes.Role).ToList())
                identity.RemoveClaim(role);
            identity.AddClaim(new Claim(DeactivatedClaim, "true"));
        }

        identity.AddClaim(new Claim(TransformedClaim, "true"));
        return Task.FromResult(cloned);
    }
}
