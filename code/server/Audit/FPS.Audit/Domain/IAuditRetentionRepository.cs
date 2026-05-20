namespace FPS.Audit.Domain;

public interface IAuditRetentionRepository
{
    Task<int> CountOlderThanAsync(string tenantId, DateTime cutoff, CancellationToken cancellationToken = default);
    Task<int> DeleteOlderThanAsync(string tenantId, DateTime cutoff, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetRangeAsync(string tenantId, DateTime from, DateTime to, int maxRecords, CancellationToken cancellationToken = default);
}
