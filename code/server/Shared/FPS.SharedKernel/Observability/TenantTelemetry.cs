using System.Diagnostics;
using FPS.SharedKernel.Identity;

namespace FPS.SharedKernel.Observability;

/// <summary>
/// PLAT005B — resolves the operator-observability <c>tenant_id</c> dimension for logs and traces.
///
/// The value is ONLY ever taken from trusted context: the validated JWT tenant claim exposed by
/// <see cref="ICurrentUser"/> (established by <c>TenantClaimsTransformation</c>, which fails closed)
/// for HTTP requests, or a Dapr-delivered event envelope's tenant for internal handlers. It is never
/// read from caller-supplied request body / query / header values. Only the tenant id is exposed —
/// no user names, emails, actor hashes, tokens, or payloads.
/// </summary>
public static class TenantTelemetry
{
    /// <summary>The structured-log / OpenTelemetry attribute name for the tenant dimension.</summary>
    public const string TagName = "tenant_id";

    /// <summary>
    /// Sentinel for requests with no trusted customer-tenant context (platform-plane, health,
    /// unauthenticated). It cannot collide with a real tenant id — tenant ids are lowercase
    /// alphanumeric plus hyphens with no underscores (see <c>TenantStorageKey.Sanitise</c>) — so an
    /// operator can select customer traffic with <c>tenant_id != "__none__"</c>.
    /// </summary>
    public const string NoTenant = "__none__";

    /// <summary>
    /// The trusted tenant id for the current principal, or <see cref="NoTenant"/> when the
    /// authenticated context carries no customer tenant. Reads ONLY the validated claim via
    /// <see cref="ICurrentUser"/>; it has no access to (and never consults) raw request input.
    /// </summary>
    public static string Resolve(ICurrentUser? currentUser)
        => currentUser is { IsAuthenticated: true, TenantId: { Length: > 0 } tenantId }
            ? tenantId
            : NoTenant;

    /// <summary>
    /// Adds the <see cref="TagName"/> span attribute for a trusted tenant. A null/blank tenant or the
    /// <see cref="NoTenant"/> sentinel is skipped, so platform-plane and no-tenant spans stay
    /// unlabelled rather than carrying a placeholder. Existing traceId/spanId correlation is untouched.
    /// </summary>
    public static void SetTenantTag(Activity? activity, string? tenantId)
    {
        if (activity is not null && !string.IsNullOrEmpty(tenantId) && tenantId != NoTenant)
            activity.SetTag(TagName, tenantId);
    }
}
