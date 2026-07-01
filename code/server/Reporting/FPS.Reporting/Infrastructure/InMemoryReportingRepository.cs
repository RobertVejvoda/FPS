using FPS.Reporting.Domain;
using System.Collections.Concurrent;

namespace FPS.Reporting.Infrastructure;

public sealed class InMemoryReportingRepository : IReportingRepository, IReportingQueryRepository
{
    // eventId -> owning tenantId. Global dedup keeps its eventId key, but tracking the owning tenant
    // lets PurgeTenantAsync drop only that tenant's markers.
    private readonly ConcurrentDictionary<string, string> _seenEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ParkingMetrics> _metrics = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FairnessRecord> _fairness = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> EventExistsAsync(string eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_seenEvents.ContainsKey(eventId));

    public Task RecordEventIdAsync(string tenantId, string eventId, CancellationToken cancellationToken = default)
    {
        _seenEvents.TryAdd(eventId, tenantId);
        return Task.CompletedTask;
    }

    public Task ApplyMetricsAsync(string tenantId, string date, string locationId, string timeSlot,
        Action<ParkingMetrics> apply, CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}:{date}:{locationId}:{timeSlot}";
        var metrics = _metrics.GetOrAdd(key, _ => new ParkingMetrics
        {
            TenantId = tenantId,
            Date = date,
            LocationId = locationId,
            TimeSlot = timeSlot
        });
        lock (metrics) { apply(metrics); }
        return Task.CompletedTask;
    }

    public Task ApplyFairnessAsync(string tenantId, string requestorRef, string date, string locationId,
        Action<FairnessRecord> apply, CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}:{requestorRef}:{date}:{locationId}";
        var record = _fairness.GetOrAdd(key, _ => new FairnessRecord
        {
            TenantId = tenantId,
            RequestorRef = requestorRef,
            Date = date,
            LocationId = locationId
        });
        lock (record) { apply(record); }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ParkingMetrics>> QueryMetricsAsync(ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var results = _metrics.Values
            .Where(m => m.TenantId == tenantId)
            .Where(m => request.DateFrom == null || string.Compare(m.Date, request.DateFrom, StringComparison.Ordinal) >= 0)
            .Where(m => request.DateTo == null || string.Compare(m.Date, request.DateTo, StringComparison.Ordinal) <= 0)
            .Where(m => request.LocationId == null || m.LocationId == request.LocationId)
            .Where(m => request.TimeSlot == null || m.TimeSlot == request.TimeSlot)
            .OrderBy(m => m.Date)
            .ThenBy(m => m.LocationId)
            .ThenBy(m => m.TimeSlot)
            .ToList();

        return Task.FromResult<IReadOnlyList<ParkingMetrics>>(results);
    }

    public Task<IReadOnlyList<FairnessRecord>> QueryFairnessAsync(FairnessQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var results = _fairness.Values
            .Where(f => f.TenantId == tenantId)
            .Where(f => request.DateFrom == null || string.Compare(f.Date, request.DateFrom, StringComparison.Ordinal) >= 0)
            .Where(f => request.DateTo == null || string.Compare(f.Date, request.DateTo, StringComparison.Ordinal) <= 0)
            .Where(f => request.LocationId == null || f.LocationId == request.LocationId)
            .GroupBy(f => f.RequestorRef)
            .Select(g => FairnessRecord.Aggregate(tenantId, g.Key, g.Sum(f => f.RequestCount), g.Sum(f => f.AllocationCount), g.Sum(f => f.RejectionCount)))
            .OrderByDescending(f => f.AllocationRate)
            .ToList();

        return Task.FromResult<IReadOnlyList<FairnessRecord>>(results);
    }

    public Task<int> AnonymiseFairnessByRequestorRefAsync(string tenantId, string requestorRef, CancellationToken cancellationToken = default)
    {
        var keys = _fairness
            .Where(kv => kv.Value.TenantId == tenantId && kv.Value.RequestorRef == requestorRef)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in keys)
            _fairness.TryRemove(key, out _);
        return Task.FromResult(keys.Count);
    }

    public Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        // Match the tenant the same way the query paths do: by the row's stored TenantId, not by
        // string-splitting the composite key. Composite keys are {tenantId}:... but the record
        // itself carries TenantId, which is the authoritative, prefix-collision-free field.
        var metricKeys = _metrics
            .Where(kv => kv.Value.TenantId == tenantId)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in metricKeys)
            _metrics.TryRemove(key, out _);

        var fairnessKeys = _fairness
            .Where(kv => kv.Value.TenantId == tenantId)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in fairnessKeys)
            _fairness.TryRemove(key, out _);

        // Drop the tenant's seen-event dedup markers so a re-seed after reset re-projects cleanly.
        var eventKeys = _seenEvents
            .Where(kv => kv.Value == tenantId)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in eventKeys)
            _seenEvents.TryRemove(key, out _);

        // Count reflects the visible Reports/Fairness rows removed; dedup markers are internal.
        return Task.FromResult(metricKeys.Count + fairnessKeys.Count);
    }
}
