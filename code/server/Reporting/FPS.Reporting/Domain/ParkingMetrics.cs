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
}
