namespace FPS.Reporting.Domain;

public sealed class FairnessRecord
{
    public string TenantId { get; init; } = string.Empty;
    public string RequestorHash { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public int RequestCount { get; private set; }
    public int AllocationCount { get; private set; }

    public double AllocationRate =>
        RequestCount > 0 ? (double)AllocationCount / RequestCount : 0.0;

    public void IncrementRequest() => RequestCount++;
    public void IncrementAllocation() => AllocationCount++;

    internal static FairnessRecord Aggregate(string tenantId, string requestorHash, int requestCount, int allocationCount) =>
        new() { TenantId = tenantId, RequestorHash = requestorHash, RequestCount = requestCount, AllocationCount = allocationCount };
}
