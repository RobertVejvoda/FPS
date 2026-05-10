using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Infrastructure.Services;

// Phase 1 stub — counts are maintained in memory per tenant/requestor/date.
// Replace with MongoDB read-side when infrastructure test phase is complete.
public sealed class InMemoryEmployeeMetricsService : IEmployeeMetricsService
{
    private readonly Dictionary<string, List<DateOnly>> allocationHistory = new();

    public Task<IReadOnlyDictionary<string, EmployeeMetrics>> GetMetricsSnapshotAsync(
        string tenantId,
        IEnumerable<string> requestorIds,
        DateOnly asOfDate,
        int lookbackDays,
        CancellationToken cancellationToken = default)
    {
        var cutoff = asOfDate.AddDays(-lookbackDays);
        var result = new Dictionary<string, EmployeeMetrics>();

        foreach (var requestorId in requestorIds)
        {
            var key = $"{tenantId}:{requestorId}";
            var recentCount = allocationHistory.TryGetValue(key, out var history)
                ? history.Count(d => d >= cutoff && d <= asOfDate)
                : 0;

            result[requestorId] = new EmployeeMetrics(requestorId, recentCount, ActivePenaltyScore: 0);
        }

        return Task.FromResult<IReadOnlyDictionary<string, EmployeeMetrics>>(result);
    }

    public Task IncrementRecentAllocationAsync(
        string tenantId,
        string requestorId,
        DateOnly allocationDate,
        CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}:{requestorId}";
        if (!allocationHistory.TryGetValue(key, out var history))
        {
            history = [];
            allocationHistory[key] = history;
        }
        history.Add(allocationDate);
        return Task.CompletedTask;
    }
}
