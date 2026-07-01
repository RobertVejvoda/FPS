namespace FPS.Configuration.Infrastructure;

/// <summary>
/// Coordinates a destructive purge of every Configuration-owned Dapr state key for a single tenant.
/// The Configuration store has no global tenant index, so location-scoped keys are discovered via
/// the per-tenant <c>config-locations</c> index maintained on the write path.
/// </summary>
public interface IConfigurationTenantPurger
{
    /// <summary>
    /// Deletes all Configuration keys for the tenant (tenant-default policy, and every location's
    /// policy override, slot list and slot-change log), then removes the location index itself.
    /// Returns the number of keys removed. Idempotent: an already-purged tenant returns 0.
    /// </summary>
    Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
