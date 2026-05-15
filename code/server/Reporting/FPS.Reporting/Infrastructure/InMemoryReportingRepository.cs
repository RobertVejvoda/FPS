using FPS.Reporting.Domain;
using System.Collections.Concurrent;

namespace FPS.Reporting.Infrastructure;

public sealed class InMemoryReportingRepository : IReportingRepository, IReportingQueryRepository
{
    private readonly ConcurrentDictionary<string, bool> _seenEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ParkingMetrics> _metrics = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FairnessRecord> _fairness = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> EventExistsAsync(string eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_seenEvents.ContainsKey(eventId));

    public Task RecordEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        _seenEvents.TryAdd(eventId, true);
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

    public Task ApplyFairnessAsync(string tenantId, string requestorHash,
        Action<FairnessRecord> apply, CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}:{requestorHash}";
        var record = _fairness.GetOrAdd(key, _ => new FairnessRecord
        {
            TenantId = tenantId,
            RequestorHash = requestorHash
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
            .OrderByDescending(f => f.AllocationRate)
            .ToList();

        return Task.FromResult<IReadOnlyList<FairnessRecord>>(results);
    }
}
