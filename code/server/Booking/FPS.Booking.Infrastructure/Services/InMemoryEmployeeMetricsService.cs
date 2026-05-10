using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Infrastructure.Services;

// Phase 1: allocation history is in-memory; penalty score reads from IPenaltyRepository.
// Replace with MongoDB read-side when infrastructure test phase is complete.
public sealed class InMemoryEmployeeMetricsService : IEmployeeMetricsService
{
    private readonly IPenaltyRepository penaltyRepository;
    private readonly Dictionary<string, List<DateOnly>> allocationHistory = new();

    public InMemoryEmployeeMetricsService(IPenaltyRepository penaltyRepository)
    {
        ArgumentNullException.ThrowIfNull(penaltyRepository);
        this.penaltyRepository = penaltyRepository;
    }

    public async Task<IReadOnlyDictionary<string, EmployeeMetrics>> GetMetricsSnapshotAsync(
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

            var penaltyScore = await GetActivePenaltyScoreAsync(tenantId, requestorId, asOfDate, cancellationToken);

            result[requestorId] = new EmployeeMetrics(requestorId, recentCount, penaltyScore);
        }

        return result;
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

    public async Task<int> GetActivePenaltyScoreAsync(
        string tenantId,
        string requestorId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var penalties = await penaltyRepository.GetActiveByRequestorAsync(
            tenantId, requestorId, asOfDate, cancellationToken);
        return penalties.Where(p => p.ExpiryDate >= asOfDate).Sum(p => p.Score);
    }
}
