using FPS.Audit.Domain;
using System.Collections.Concurrent;

namespace FPS.Audit.Infrastructure;

// Phase 1 stub — replace with MongoDB append-only collection.
public sealed class InMemoryAuditRepository : IAuditRepository
{
    private readonly ConcurrentDictionary<string, AuditRecord> store = new();

    public Task<bool> ExistsAsync(string sourceEventId, CancellationToken cancellationToken = default)
        => Task.FromResult(store.ContainsKey(sourceEventId));

    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        store.TryAdd(record.SourceEventId, record);
        return Task.CompletedTask;
    }
}
