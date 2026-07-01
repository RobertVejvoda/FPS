using FPS.Audit.Domain;
using System.Collections.Concurrent;

namespace FPS.Audit.Infrastructure;

// Phase 1 stub — replace with MongoDB append-only collection.
public sealed class InMemoryAuditRepository : IAuditRepository, IAuditQueryRepository, IAuditRetentionRepository
{
    private readonly ConcurrentDictionary<string, AuditRecord> store = new();

    public Task<bool> ExistsAsync(string sourceEventId, string tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(store.TryGetValue(sourceEventId, out var r) && r.TenantId == tenantId);

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
            .Where(r => query.ActorRef is null || r.ActorHash?.StartsWith(query.ActorRef, StringComparison.OrdinalIgnoreCase) == true)
            .Where(r => query.OccurredAfter is null || r.OccurredAt >= query.OccurredAfter)
            .Where(r => query.OccurredBefore is null || r.OccurredAt <= query.OccurredBefore)
            .Where(r => query.Action is null || r.Action == query.Action)
            .Where(r => query.Result is null || r.Result == query.Result)
            .Where(r => query.ReasonCode is null || r.ReasonCode == query.ReasonCode)
            .Where(r => query.TraceId is null || r.TraceId == query.TraceId || r.ProcessingTraceId == query.TraceId)
            .Where(r => query.Category is null || MatchesCategory(r, query.Category.Value))
            .OrderByDescending(r => r.OccurredAt)
            .ToList();

        var totalCount = filtered.Count;
        var items = filtered
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToList();

        return Task.FromResult(((IReadOnlyList<AuditRecord>)items, totalCount));
    }

    private static bool MatchesCategory(AuditRecord record, ActivityCategory category)
    {
        return category switch
        {
            ActivityCategory.All => true,
            ActivityCategory.BookingLifecycle => record.EventType.StartsWith("booking.request", StringComparison.OrdinalIgnoreCase)
                || record.EventType.Equals("booking.slotAllocated", StringComparison.OrdinalIgnoreCase)
                || record.EventType.Equals("booking.usageConfirmed", StringComparison.OrdinalIgnoreCase)
                || record.EventType.Equals("booking.noShowRecorded", StringComparison.OrdinalIgnoreCase),
            ActivityCategory.DrawEvents => record.EventType.StartsWith("booking.draw", StringComparison.OrdinalIgnoreCase),
            ActivityCategory.PolicyChanges => record.EventType.Contains("policy", StringComparison.OrdinalIgnoreCase)
                || record.EventType.Contains("capacity", StringComparison.OrdinalIgnoreCase)
                || record.EventType.Contains("configuration", StringComparison.OrdinalIgnoreCase),
            ActivityCategory.Notifications => record.EventType.Contains("notification", StringComparison.OrdinalIgnoreCase),
            ActivityCategory.PrivacyErasure => record.EventType.StartsWith("privacy.erasure", StringComparison.OrdinalIgnoreCase),
            ActivityCategory.ManualCorrections => record.EventType.Contains("manualCorrection", StringComparison.OrdinalIgnoreCase)
                || record.EventType.Contains("correction", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    public Task<int> CountOlderThanAsync(string tenantId, DateTime cutoff, CancellationToken cancellationToken = default)
    {
        var count = store.Values.Count(r => r.TenantId == tenantId && r.OccurredAt < cutoff);
        return Task.FromResult(count);
    }

    public Task<int> DeleteOlderThanAsync(string tenantId, DateTime cutoff, CancellationToken cancellationToken = default)
    {
        var toDelete = store.Values
            .Where(r => r.TenantId == tenantId && r.OccurredAt < cutoff)
            .Select(r => r.SourceEventId)
            .ToList();

        foreach (var id in toDelete)
            store.TryRemove(id, out _);

        return Task.FromResult(toDelete.Count);
    }

    public Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var toDelete = store.Values
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.SourceEventId)
            .ToList();

        foreach (var id in toDelete)
            store.TryRemove(id, out _);

        return Task.FromResult(toDelete.Count);
    }

    public Task<IReadOnlyList<AuditRecord>> GetRangeAsync(
        string tenantId, DateTime from, DateTime to, int maxRecords, CancellationToken cancellationToken = default)
    {
        var records = store.Values
            .Where(r => r.TenantId == tenantId && r.OccurredAt >= from && r.OccurredAt <= to)
            .OrderBy(r => r.OccurredAt)
            .ThenBy(r => r.AuditRecordId)
            .Take(maxRecords)
            .ToList();

        return Task.FromResult((IReadOnlyList<AuditRecord>)records);
    }
}
