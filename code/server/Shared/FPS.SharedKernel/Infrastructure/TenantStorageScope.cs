using System.Text.RegularExpressions;

namespace FPS.SharedKernel.Infrastructure;

/// <summary>
/// Provider-neutral tenant storage scope (PLAT002). Produces deterministic, sanitised
/// collection / partition / schema-safe names and Dapr state-key prefixes for one tenant,
/// derived <b>only</b> from a trusted tenant id. Reuses <see cref="TenantStorageKey"/>
/// sanitisation — callers never supply storage names directly.
///
/// See docs/production/tenant-storage-contract.md and the "Tenant-scoped storage boundary"
/// decision in docs/versions-and-decisions.md.
/// </summary>
public static class TenantStorageScope
{
    /// <summary>
    /// Bounded contexts that persist tenant data — the per-service scopes recorded as
    /// provisioning evidence and addressed by a tenant purge. "reporting" is legacy.
    /// </summary>
    public static readonly IReadOnlyList<string> Services =
        ["customer", "booking", "notification", "profile", "audit", "configuration", "datahub", "reporting"];

    private static readonly Regex ServicePattern = new(@"^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled);

    /// <summary>
    /// Maximum generated identifier length. PostgreSQL identifiers are capped at 63 bytes; this
    /// is the binding limit across the supported stores (MongoDB collection names allow more).
    /// </summary>
    public const int MaxNameLength = 63;

    /// <summary>
    /// Deterministic collection / partition / schema-safe name for a service's tenant data:
    /// <c>fps-{tenantId}-{service}</c>. Lowercase and hyphenated, so it is safe as a MongoDB
    /// collection, a PostgreSQL schema, an object-storage prefix, or a DNS label. When the
    /// tenant id is long enough that the full name would exceed <see cref="MaxNameLength"/>, the
    /// tenant segment is deterministically truncated and a short hash suffix keeps it unique.
    /// </summary>
    public static string Collection(string service, string tenantId)
    {
        var tenant = TenantStorageKey.Sanitise(tenantId);
        var svc = NormaliseService(service);
        var name = $"fps-{tenant}-{svc}";
        if (name.Length <= MaxNameLength)
            return name;

        var hash = ShortHash(tenant);
        var fixedLength = "fps-".Length + 1 + hash.Length + 1 + svc.Length; // fps-{tPrefix}-{hash}-{svc}
        var budget = MaxNameLength - fixedLength;
        var prefix = budget > 0 ? tenant[..Math.Min(tenant.Length, budget)].TrimEnd('-') : string.Empty;
        return prefix.Length > 0 ? $"fps-{prefix}-{hash}-{svc}" : $"fps-{hash}-{svc}";
    }

    private static string ShortHash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..8].ToLowerInvariant();

    /// <summary>
    /// The Dapr state-key prefix that scopes a tenant's keys for an entity type:
    /// <c>{entityType}:{tenantId}:</c>. A backup or purge over this prefix covers exactly
    /// that tenant's data for the entity type.
    /// </summary>
    public static string KeyPrefix(string entityType, string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        return $"{entityType}:{TenantStorageKey.Sanitise(tenantId)}:";
    }

    /// <summary>The <c>:{tenantId}:</c> segment that every tenant-scoped key contains.</summary>
    public static string KeySegment(string tenantId) => $":{TenantStorageKey.Sanitise(tenantId)}:";

    private static string NormaliseService(string service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        var s = service.Trim().ToLowerInvariant();
        if (!ServicePattern.IsMatch(s))
            throw new ArgumentException(
                $"Service name '{service}' is not collection-safe (lowercase alphanumeric + hyphens).", nameof(service));
        return s;
    }
}
