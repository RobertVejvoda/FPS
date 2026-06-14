namespace FPS.Reporting.Domain;

public sealed class FairnessRecord
{
    public string TenantId { get; init; } = string.Empty;
    // Resolvable requestor reference — the same id Profile uses as its user key
    // and that Booking emits on its events. Stored raw (not hashed) so the HR
    // Reports surface can call /profile/hr/display-names to surface employee
    // names instead of opaque hash prefixes (issue #474). Long IDs are never
    // shown directly in the UI — the page falls back to displayRequestorRef
    // when no display name is available, matching HR Operations.
    public string RequestorRef { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public int RequestCount { get; private set; }
    public int AllocationCount { get; private set; }
    public int RejectionCount { get; private set; }

    public double AllocationRate =>
        RequestCount > 0 ? (double)AllocationCount / RequestCount : 0.0;

    public void IncrementRequest() => RequestCount++;
    public void IncrementAllocation() => AllocationCount++;
    public void IncrementRejection() => RejectionCount++;

    internal static FairnessRecord Aggregate(string tenantId, string requestorRef, int requestCount, int allocationCount, int rejectionCount = 0) =>
        new() { TenantId = tenantId, RequestorRef = requestorRef, RequestCount = requestCount, AllocationCount = allocationCount, RejectionCount = rejectionCount };
}
