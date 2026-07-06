namespace FPS.Reporting.Domain;

public sealed class ParkingMetrics
{
    public string TenantId { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string TimeSlot { get; init; } = string.Empty;
    public int DemandCount { get; private set; }
    public int AllocationCount { get; private set; }
    public int RejectionCount { get; private set; }
    public int CancellationCount { get; private set; }
    public int NoShowCount { get; private set; }
    public int PenaltyCount { get; private set; }
    public int UsageConfirmedCount { get; private set; }
    public Dictionary<string, int> RejectionByReason { get; } = new(StringComparer.OrdinalIgnoreCase);

    public double AllocationRate =>
        DemandCount > 0 ? (double)AllocationCount / DemandCount : 0.0;

    public void IncrementDemand() => DemandCount++;
    public void IncrementAllocation() => AllocationCount++;
    public void IncrementRejection(string? reasonCode)
    {
        RejectionCount++;
        if (!string.IsNullOrEmpty(reasonCode))
            RejectionByReason[reasonCode] = RejectionByReason.GetValueOrDefault(reasonCode) + 1;
    }
    public void IncrementCancellation() => CancellationCount++;
    public void IncrementNoShow() => NoShowCount++;
    public void IncrementPenalty() => PenaltyCount++;
    public void IncrementUsageConfirmed() => UsageConfirmedCount++;

    /// <summary>
    /// #763: build a row directly from already-aggregated counts (e.g. projected from DataHub's
    /// durable read models) rather than by replaying events. UsageConfirmed is not part of any report
    /// contract, so it is not projected here.
    /// </summary>
    internal static ParkingMetrics Project(
        string tenantId, string date, string locationId, string timeSlot,
        int demand, int allocation, int rejection, int cancellation, int noShow, int penalty,
        IReadOnlyDictionary<string, int>? rejectionByReason = null)
    {
        var metrics = new ParkingMetrics
        {
            TenantId = tenantId,
            Date = date,
            LocationId = locationId,
            TimeSlot = timeSlot,
            DemandCount = demand,
            AllocationCount = allocation,
            RejectionCount = rejection,
            CancellationCount = cancellation,
            NoShowCount = noShow,
            PenaltyCount = penalty,
        };
        if (rejectionByReason is not null)
            foreach (var (reason, count) in rejectionByReason)
                metrics.RejectionByReason[reason] = count;
        return metrics;
    }
}
