using Dapr.Client;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FPS.Booking.Infrastructure.Services;

// Allocation history stored as a list of ISO date strings per user under one key.
// Design: one key per tenant+user (not one per date) limits Dapr round-trips to 1
// per participant during GetMetricsSnapshotAsync regardless of lookback window length.
// Window filtering happens in-memory after the single read.
public sealed class DaprEmployeeMetricsService : IEmployeeMetricsService
{
    private readonly DaprClient daprClient;
    private readonly IServiceScopeFactory scopeFactory;
    private const string StoreName = "bookingstore";

    public DaprEmployeeMetricsService(DaprClient daprClient, IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        this.daprClient = daprClient;
        this.scopeFactory = scopeFactory;
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
            var dates = await daprClient.GetStateAsync<List<string>>(
                StoreName, MetricsKey(tenantId, requestorId), cancellationToken: cancellationToken) ?? [];

            var recentCount = dates.Count(d =>
            {
                var date = DateOnly.Parse(d);
                return date >= cutoff && date <= asOfDate;
            });

            var penaltyScore = await GetActivePenaltyScoreAsync(tenantId, requestorId, asOfDate, cancellationToken);
            result[requestorId] = new EmployeeMetrics(requestorId, recentCount, penaltyScore);
        }

        return result;
    }

    public async Task IncrementRecentAllocationAsync(
        string tenantId,
        string requestorId,
        DateOnly allocationDate,
        CancellationToken cancellationToken = default)
    {
        var key = MetricsKey(tenantId, requestorId);
        var dates = await daprClient.GetStateAsync<List<string>>(StoreName, key, cancellationToken: cancellationToken) ?? [];
        dates.Add(allocationDate.ToString("yyyy-MM-dd"));
        await daprClient.SaveStateAsync(StoreName, key, dates, cancellationToken: cancellationToken);
    }

    public async Task<int> GetActivePenaltyScoreAsync(
        string tenantId,
        string requestorId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var penaltyRepository = scope.ServiceProvider.GetRequiredService<IPenaltyRepository>();
        var penalties = await penaltyRepository.GetActiveByRequestorAsync(
            tenantId, requestorId, asOfDate, cancellationToken);
        return penalties.Where(p => p.ExpiryDate >= asOfDate).Sum(p => p.Score);
    }

    private static string MetricsKey(string tenantId, string requestorId)
        => TenantStorageKey.For("metrics", tenantId, requestorId);
}
