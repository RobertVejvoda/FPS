namespace FPS.SharedKernel.Infrastructure;

/// <summary>
/// What a single-tenant purge targets — derived from the tenant id <b>only</b>, never from
/// caller-supplied storage names. A purger handed this scope can address exactly one tenant's
/// storage: the key segment and every collection name contain only the sanitised tenant id.
/// </summary>
public sealed record TenantPurgeScope
{
    /// <summary>Sanitised tenant id (rejects blank / invalid / reserved at construction).</summary>
    public required string TenantId { get; init; }

    /// <summary>The <c>:{tenantId}:</c> segment every tenant-scoped Dapr key must contain.</summary>
    public required string KeySegment { get; init; }

    /// <summary>Per-service collection / partition names for this tenant.</summary>
    public required IReadOnlyDictionary<string, string> Collections { get; init; }

    /// <summary>
    /// Builds the scope for a tenant. Throws <see cref="ArgumentException"/> on a missing,
    /// blank, or contract-invalid tenant id (via <see cref="TenantStorageKey.Sanitise"/>).
    /// </summary>
    public static TenantPurgeScope For(string tenantId)
    {
        var safe = TenantStorageKey.Sanitise(tenantId);
        return new TenantPurgeScope
        {
            TenantId = safe,
            KeySegment = TenantStorageScope.KeySegment(safe),
            Collections = TenantStorageScope.Services.ToDictionary(
                s => s, s => TenantStorageScope.Collection(s, safe), StringComparer.Ordinal),
        };
    }
}

/// <summary>
/// Per-service purge hook. Each store-owning service implements it to delete its own tenant data
/// for a <see cref="TenantPurgeScope"/>. A store that holds immutable evidence (audit records)
/// sets <see cref="IsImmutableEvidence"/> so it is only purged for an explicit sandbox/demo reset;
/// normal GDPR erasure stays a separate path.
/// </summary>
public interface ITenantStorePurger
{
    /// <summary>The bounded-context service this purger owns (e.g. "booking").</summary>
    string Service { get; }

    /// <summary>True for append-only / evidence stores that must not be purged outside a sandbox reset.</summary>
    bool IsImmutableEvidence { get; }

    /// <summary>Deletes this service's data for the scope; returns the count removed.</summary>
    Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct);
}

/// <summary>
/// Platform-only orchestration of a single-tenant purge (PLAT002). Builds the scope from the
/// tenant id (rejecting unsafe input), then invokes each registered <see cref="ITenantStorePurger"/>.
/// Immutable-evidence stores are skipped unless <paramref name="sandboxReset"/> is set, so a
/// normal purge can never delete audit evidence. The orchestrator never accepts storage names
/// from callers — only a tenant id.
/// </summary>
public sealed class TenantPurgeOrchestrator(IEnumerable<ITenantStorePurger> purgers)
{
    /// <summary>
    /// Purges one tenant across the registered store purgers. Returns the per-service count
    /// removed (immutable-evidence services omitted unless <paramref name="sandboxReset"/>).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> PurgeAsync(
        string tenantId, bool sandboxReset, CancellationToken ct = default)
    {
        var scope = TenantPurgeScope.For(tenantId); // throws on blank/invalid — fail closed
        var results = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var purger in purgers)
        {
            if (purger.IsImmutableEvidence && !sandboxReset)
                continue; // audit / immutable evidence is never purged outside a sandbox reset
            results[purger.Service] = await purger.PurgeAsync(scope, sandboxReset, ct);
        }
        return results;
    }
}
