using Dapr.Client;
using FPS.Audit.Application.Privacy;
using FPS.Audit.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Audit.Infrastructure;

public sealed class DaprAuditRepository : IAuditRepository, IAuditQueryRepository, IAuditRetentionRepository
{
    private readonly DaprClient daprClient;
    private const string StoreName = "auditstore";

    public DaprAuditRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<bool> ExistsAsync(string sourceEventId, string tenantId, CancellationToken ct = default)
        => await daprClient.GetStateAsync<bool>(StoreName, SrcKey(tenantId, sourceEventId), cancellationToken: ct);

    public async Task AppendAsync(AuditRecord record, CancellationToken ct = default)
    {
        var srcKey = SrcKey(record.TenantId, record.SourceEventId);
        if (await daprClient.GetStateAsync<bool>(StoreName, srcKey, cancellationToken: ct))
            return;

        // Write record first, then make it queryable via the index, then mark
        // the source event as processed. This ordering ensures that if the index
        // write fails the record is invisible and the next retry can re-append it.
        await daprClient.SaveStateAsync(StoreName, RecordKey(record.TenantId, record.AuditRecordId.ToString()), record, cancellationToken: ct);
        await AddToIndexAsync(record.TenantId, record.AuditRecordId.ToString(), ct);
        await daprClient.SaveStateAsync(StoreName, srcKey, true, cancellationToken: ct);
    }

    public async Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> QueryAsync(
        AuditQueryRequest query, string tenantId, CancellationToken ct = default)
    {
        var records = await LoadAllAsync(tenantId, ct);
        var filtered = records
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
        var items = filtered.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        return (items, totalCount);
    }

    public async Task<int> CountOlderThanAsync(string tenantId, DateTime cutoff, CancellationToken ct = default)
    {
        var records = await LoadAllAsync(tenantId, ct);
        return records.Count(r => r.OccurredAt < cutoff);
    }

    public async Task<int> DeleteOlderThanAsync(string tenantId, DateTime cutoff, CancellationToken ct = default)
    {
        var indexKey = IndexKey(tenantId);
        var index = await daprClient.GetStateAsync<List<string>>(StoreName, indexKey, cancellationToken: ct) ?? [];

        var toDelete = new List<string>();
        var toKeep = new List<string>();

        foreach (var id in index)
        {
            var record = await daprClient.GetStateAsync<AuditRecord>(StoreName, RecordKey(tenantId, id), cancellationToken: ct);
            if (record is not null && record.OccurredAt < cutoff)
                toDelete.Add(id);
            else
                toKeep.Add(id);
        }

        foreach (var id in toDelete)
            await daprClient.DeleteStateAsync(StoreName, RecordKey(tenantId, id), cancellationToken: ct);

        if (toDelete.Count > 0)
            await daprClient.SaveStateAsync(StoreName, indexKey, toKeep, cancellationToken: ct);

        return toDelete.Count;
    }

    public async Task<int> PurgeTenantAsync(string tenantId, CancellationToken ct = default)
    {
        var indexKey = IndexKey(tenantId);
        var index = await daprClient.GetStateAsync<List<string>>(StoreName, indexKey, cancellationToken: ct) ?? [];

        var removed = 0;
        foreach (var id in index)
        {
            // Load the record to recover its SourceEventId so the dedup marker is deleted too.
            var record = await daprClient.GetStateAsync<AuditRecord>(StoreName, RecordKey(tenantId, id), cancellationToken: ct);
            await daprClient.DeleteStateAsync(StoreName, RecordKey(tenantId, id), cancellationToken: ct);
            if (record is not null)
            {
                await daprClient.DeleteStateAsync(StoreName, SrcKey(tenantId, record.SourceEventId), cancellationToken: ct);
                removed++;
            }
        }

        await daprClient.DeleteStateAsync(StoreName, indexKey, cancellationToken: ct);
        return removed;
    }

    public async Task<IReadOnlyList<AuditRecord>> GetRangeAsync(
        string tenantId, DateTime from, DateTime to, int maxRecords, CancellationToken ct = default)
    {
        var records = await LoadAllAsync(tenantId, ct);
        return records
            .Where(r => r.OccurredAt >= from && r.OccurredAt <= to)
            .OrderBy(r => r.OccurredAt)
            .ThenBy(r => r.AuditRecordId)
            .Take(maxRecords)
            .ToList();
    }

    private async Task<List<AuditRecord>> LoadAllAsync(string tenantId, CancellationToken ct)
    {
        var index = await daprClient.GetStateAsync<List<string>>(StoreName, IndexKey(tenantId), cancellationToken: ct) ?? [];
        var records = new List<AuditRecord>(index.Count);
        foreach (var id in index)
        {
            var r = await daprClient.GetStateAsync<AuditRecord>(StoreName, RecordKey(tenantId, id), cancellationToken: ct);
            if (r is not null) records.Add(r);
        }
        return records;
    }

    private async Task AddToIndexAsync(string tenantId, string recordId, CancellationToken ct)
    {
        var key = IndexKey(tenantId);
        var index = await daprClient.GetStateAsync<List<string>>(StoreName, key, cancellationToken: ct) ?? [];
        if (!index.Contains(recordId, StringComparer.Ordinal))
        {
            index.Add(recordId);
            await daprClient.SaveStateAsync(StoreName, key, index, cancellationToken: ct);
        }
    }

    private static bool MatchesCategory(AuditRecord r, ActivityCategory category) => category switch
    {
        ActivityCategory.All => true,
        ActivityCategory.BookingLifecycle =>
            r.EventType.StartsWith("booking.request", StringComparison.OrdinalIgnoreCase)
            || r.EventType.Equals("booking.slotAllocated", StringComparison.OrdinalIgnoreCase)
            || r.EventType.Equals("booking.usageConfirmed", StringComparison.OrdinalIgnoreCase)
            || r.EventType.Equals("booking.noShowRecorded", StringComparison.OrdinalIgnoreCase),
        ActivityCategory.DrawEvents => r.EventType.StartsWith("booking.draw", StringComparison.OrdinalIgnoreCase),
        ActivityCategory.PolicyChanges =>
            r.EventType.Contains("policy", StringComparison.OrdinalIgnoreCase)
            || r.EventType.Contains("capacity", StringComparison.OrdinalIgnoreCase)
            || r.EventType.Contains("configuration", StringComparison.OrdinalIgnoreCase),
        ActivityCategory.Notifications => r.EventType.Contains("notification", StringComparison.OrdinalIgnoreCase),
        ActivityCategory.PrivacyErasure => r.EventType.StartsWith("privacy.erasure", StringComparison.OrdinalIgnoreCase),
        ActivityCategory.ManualCorrections =>
            r.EventType.Contains("manualCorrection", StringComparison.OrdinalIgnoreCase)
            || r.EventType.Contains("correction", StringComparison.OrdinalIgnoreCase),
        _ => true
    };

    private static string RecordKey(string tenantId, string recordId) => TenantStorageKey.For("audit", tenantId, recordId);
    private static string IndexKey(string tenantId) => TenantStorageKey.For("audit-index", tenantId, "all");
    private static string SrcKey(string tenantId, string sourceEventId) => TenantStorageKey.For("audit-src", tenantId, sourceEventId);
}
