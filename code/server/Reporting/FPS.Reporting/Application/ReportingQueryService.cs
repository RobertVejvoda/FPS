using FPS.Reporting.Domain;

namespace FPS.Reporting.Application;

public sealed class ReportingQueryService(IReportingQueryRepository repository)
{
    public async Task<ParkingSummaryResponse> GetSummaryAsync(ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var items = await repository.QueryMetricsAsync(request, tenantId, cancellationToken);
        return new ParkingSummaryResponse(items.Select(ParkingMetricsSummary.From).ToList());
    }

    public async Task<FairnessResponse> GetFairnessAsync(FairnessQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var items = await repository.QueryFairnessAsync(request, tenantId, cancellationToken);
        return new FairnessResponse(items.Select(FairnessEntry.From).ToList());
    }

    public async Task<DashboardResponse> GetDashboardAsync(ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var items = await repository.QueryMetricsAsync(request, tenantId, cancellationToken);

        var totalDemand = items.Sum(m => m.DemandCount);
        var totalAllocations = items.Sum(m => m.AllocationCount);
        var totalRejections = items.Sum(m => m.RejectionCount);
        var totalCancellations = items.Sum(m => m.CancellationCount);
        var totalNoShows = items.Sum(m => m.NoShowCount);
        var totalPenalties = items.Sum(m => m.PenaltyCount);
        var overallRate = totalDemand > 0 ? (double)totalAllocations / totalDemand : 0.0;

        var rejectionsByReason = items
            .SelectMany(m => m.RejectionByReason)
            .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

        var dailyTrend = items
            .GroupBy(m => m.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var demand = g.Sum(m => m.DemandCount);
                var allocated = g.Sum(m => m.AllocationCount);
                return new DailyTrendEntry(g.Key, demand, allocated,
                    demand > 0 ? (double)allocated / demand : 0.0);
            })
            .ToList();

        return new DashboardResponse(
            totalDemand, totalAllocations, totalRejections,
            totalCancellations, totalNoShows, totalPenalties,
            overallRate, rejectionsByReason, dailyTrend);
    }

    public async Task<string> GetSummaryCsvAsync(ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var items = await repository.QueryMetricsAsync(request, tenantId, cancellationToken);
        return CsvExport.FromMetrics(items);
    }

    public async Task<UtilizationResponse> GetUtilizationAsync(ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var items = await repository.QueryMetricsAsync(request, tenantId, cancellationToken);
        var entries = items
            .GroupBy(m => m.LocationId)
            .Select(g =>
            {
                var demand = g.Sum(m => m.DemandCount);
                var allocated = g.Sum(m => m.AllocationCount);
                return new UtilizationEntry(
                    g.Key,
                    demand,
                    allocated,
                    g.Sum(m => m.RejectionCount),
                    g.Sum(m => m.CancellationCount),
                    g.Sum(m => m.NoShowCount),
                    demand > 0 ? (double)allocated / demand : 0.0);
            })
            .OrderBy(e => e.LocationId)
            .ToList();
        return new UtilizationResponse(entries);
    }

    public async Task<ReasonCodeResponse> GetReasonCodeReportAsync(ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var items = await repository.QueryMetricsAsync(request, tenantId, cancellationToken);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in items)
        {
            foreach (var (reason, count) in m.RejectionByReason)
                counts[reason] = counts.GetValueOrDefault(reason) + count;

            if (m.CancellationCount > 0)
                counts["cancellation"] = counts.GetValueOrDefault("cancellation") + m.CancellationCount;
            if (m.NoShowCount > 0)
                counts["no_show"] = counts.GetValueOrDefault("no_show") + m.NoShowCount;
        }

        var totalDemand = items.Sum(m => m.DemandCount);
        var entries = counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => new ReasonCodeEntry(
                kv.Key, kv.Value,
                totalDemand > 0 ? (double)kv.Value / totalDemand : 0.0))
            .ToList();
        return new ReasonCodeResponse(entries, totalDemand);
    }

    public async Task<string> GetAllocationOutcomesCsvAsync(ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var items = await repository.QueryMetricsAsync(request, tenantId, cancellationToken);
        return CsvExport.FromAllocationOutcomes(items);
    }

    public async Task<EmployeeImpactResponse> GetEmployeeImpactAsync(FairnessQueryRequest request, string tenantId, int minRejections = 2, CancellationToken cancellationToken = default)
    {
        var fairnessRecords = await repository.QueryFairnessAsync(request, tenantId, cancellationToken);
        var impactedEmployees = fairnessRecords
            .Where(f => f.RejectionCount >= minRejections)
            .OrderByDescending(f => f.RejectionCount)
            .ThenBy(f => f.RequestorHash)
            .Select(f => new EmployeeImpactEntry(
                f.RequestorHash,
                f.RequestCount,
                f.RejectionCount,
                f.AllocationCount))
            .ToList();
        return new EmployeeImpactResponse(impactedEmployees, minRejections);
    }

    public async Task<OperationalExceptionsResponse> GetOperationalExceptionsAsync(ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var items = await repository.QueryMetricsAsync(request, tenantId, cancellationToken);
        var exceptions = new List<OperationalExceptionEntry>();

        var byDateLocation = items
            .GroupBy(m => (m.Date, m.LocationId))
            .OrderBy(g => g.Key.Date)
            .ThenBy(g => g.Key.LocationId);

        foreach (var g in byDateLocation)
        {
            var demand = g.Sum(m => m.DemandCount);
            var allocations = g.Sum(m => m.AllocationCount);
            var rejections = g.Sum(m => m.RejectionCount);

            if (demand == 0) continue;

            if (allocations == 0 && rejections == 0)
            {
                exceptions.Add(new OperationalExceptionEntry(
                    g.Key.Date, g.Key.LocationId,
                    "demand_no_allocations",
                    "Demand recorded but no allocations or rejections — draw may not have run.",
                    demand, allocations, rejections));
            }
            else if (allocations == 0 && rejections == demand)
            {
                exceptions.Add(new OperationalExceptionEntry(
                    g.Key.Date, g.Key.LocationId,
                    "all_rejected",
                    "All requests rejected with zero allocations — draw completed but no spots were assigned.",
                    demand, allocations, rejections));
            }
        }

        return new OperationalExceptionsResponse(exceptions);
    }
}

public sealed record ParkingMetricsSummary(
    string Date,
    string LocationId,
    string TimeSlot,
    int DemandCount,
    int AllocationCount,
    double AllocationRate,
    int RejectionCount,
    int CancellationCount,
    int NoShowCount,
    int PenaltyCount,
    IReadOnlyDictionary<string, int> RejectionByReason)
{
    public static ParkingMetricsSummary From(ParkingMetrics m) => new(
        m.Date, m.LocationId, m.TimeSlot,
        m.DemandCount, m.AllocationCount, m.AllocationRate,
        m.RejectionCount, m.CancellationCount, m.NoShowCount, m.PenaltyCount,
        m.RejectionByReason);
}

public sealed record ParkingSummaryResponse(IReadOnlyList<ParkingMetricsSummary> Items);

public sealed record FairnessEntry(string RequestorHash, int RequestCount, int AllocationCount, double AllocationRate)
{
    public static FairnessEntry From(FairnessRecord r) =>
        new(r.RequestorHash, r.RequestCount, r.AllocationCount, r.AllocationRate);
}

public sealed record FairnessResponse(IReadOnlyList<FairnessEntry> Items);

public sealed record DailyTrendEntry(string Date, int Demand, int Allocations, double AllocationRate);

public sealed record DashboardResponse(
    int TotalDemand,
    int TotalAllocations,
    int TotalRejections,
    int TotalCancellations,
    int TotalNoShows,
    int TotalPenalties,
    double OverallAllocationRate,
    IReadOnlyDictionary<string, int> RejectionsByReason,
    IReadOnlyList<DailyTrendEntry> DailyTrend);

public sealed record UtilizationEntry(
    string LocationId,
    int TotalDemand,
    int TotalAllocations,
    int TotalRejections,
    int TotalCancellations,
    int TotalNoShows,
    double AllocationRate);

public sealed record UtilizationResponse(IReadOnlyList<UtilizationEntry> Items);

public sealed record ReasonCodeEntry(string ReasonCode, int Count, double RateOfDemand);

public sealed record ReasonCodeResponse(IReadOnlyList<ReasonCodeEntry> Items, int TotalDemand);

public sealed record EmployeeImpactEntry(
    string RequestorHash,
    int TotalRequests,
    int TotalRejections,
    int TotalAllocations);

public sealed record EmployeeImpactResponse(IReadOnlyList<EmployeeImpactEntry> Items, int MinRejectionThreshold);

public sealed record OperationalExceptionEntry(
    string Date,
    string LocationId,
    string ExceptionType,
    string Description,
    int TotalDemand,
    int TotalAllocations,
    int TotalRejections);

public sealed record OperationalExceptionsResponse(IReadOnlyList<OperationalExceptionEntry> Items);

public static class CsvExport
{
    public static string FromMetrics(IEnumerable<ParkingMetrics> metrics)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date,LocationId,TimeSlot,Demand,Allocations,AllocationRate,Rejections,Cancellations,NoShows,Penalties");
        foreach (var m in metrics)
        {
            sb.AppendLine(string.Join(",",
                Escape(m.Date), Escape(m.LocationId), Escape(m.TimeSlot),
                m.DemandCount, m.AllocationCount,
                m.AllocationRate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                m.RejectionCount, m.CancellationCount, m.NoShowCount, m.PenaltyCount));
        }
        return sb.ToString();
    }

    public static string FromAllocationOutcomes(IEnumerable<ParkingMetrics> metrics)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date,LocationId,TimeSlot,Demand,Allocations,AllocationRate,Rejections,Cancellations,NoShows");
        foreach (var m in metrics)
        {
            sb.AppendLine(string.Join(",",
                Escape(m.Date), Escape(m.LocationId), Escape(m.TimeSlot),
                m.DemandCount, m.AllocationCount,
                m.AllocationRate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                m.RejectionCount, m.CancellationCount, m.NoShowCount));
        }
        return sb.ToString();
    }

    public static string Escape(string value)
    {
        // Normalize line endings — embedded CR/LF would break CSV rows.
        value = value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

        // Neutralize spreadsheet formula-injection: prefix with apostrophe so
        // Excel/Sheets treats the cell as literal text.
        if (value.Length > 0 && "=+-@|".IndexOf(value[0]) >= 0)
            value = "'" + value;

        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
