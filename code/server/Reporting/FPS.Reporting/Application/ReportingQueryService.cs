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
