using FPS.Audit.Domain;
using System.Collections.Concurrent;

namespace FPS.Audit.Infrastructure;

// Phase 1 stub — replace with MongoDB append-only collection.
public sealed class InMemoryAuditRepository : IAuditRepository, IAuditQueryRepository
{
    private readonly ConcurrentDictionary<string, AuditRecord> store = new();

    public Task<bool> ExistsAsync(string sourceEventId, CancellationToken cancellationToken = default)
        => Task.FromResult(store.ContainsKey(sourceEventId));

    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        store.TryAdd(record.SourceEventId, record);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> QueryAsync(
        AuditQueryRequest query, string tenantId, CancellationToken cancellationToken = default)
    {
        var filtered = store.Values
            .Where(r => r.TenantId == tenantId)
            .Where(r => query.EntityType is null || r.EntityType == query.EntityType)
            .Where(r => query.EntityId is null || r.EntityId == query.EntityId)
            .Where(r => query.EventType is null || r.EventType == query.EventType)
            .Where(r => query.ActorHash is null || r.ActorHash == query.ActorHash)
            .Where(r => query.OccurredAfter is null || r.OccurredAt >= query.OccurredAfter)
            .Where(r => query.OccurredBefore is null || r.OccurredAt <= query.OccurredBefore)
            .OrderByDescending(r => r.OccurredAt)
            .ToList();

        var totalCount = filtered.Count;
        var items = filtered
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToList();

        return Task.FromResult(((IReadOnlyList<AuditRecord>)items, totalCount));
    }
}
