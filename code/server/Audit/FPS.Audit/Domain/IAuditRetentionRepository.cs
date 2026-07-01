namespace FPS.Audit.Domain;

public interface IAuditRetentionRepository
{
    Task<int> CountOlderThanAsync(string tenantId, DateTime cutoff, CancellationToken cancellationToken = default);
    Task<int> DeleteOlderThanAsync(string tenantId, DateTime cutoff, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetRangeAsync(string tenantId, DateTime from, DateTime to, int maxRecords, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every audit record, dedup marker and the index for a single tenant (sandbox/demo reset only).
    /// Idempotent: an empty tenant returns 0. Returns the number of records removed.
    /// </summary>
    Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
