namespace FPS.Reporting.Domain;

public interface IReportingRepository
{
    Task<bool> EventExistsAsync(string eventId, CancellationToken cancellationToken = default);
    Task RecordEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task ApplyMetricsAsync(string tenantId, string date, string locationId, string timeSlot,
        Action<ParkingMetrics> apply, CancellationToken cancellationToken = default);
    Task ApplyFairnessAsync(string tenantId, string requestorRef, string date, string locationId,
        Action<FairnessRecord> apply, CancellationToken cancellationToken = default);

    // Erasure removes all fairness rows for the given requestor reference.
    // The caller now passes the raw user id (the same one Profile uses); rows
    // are matched against FairnessRecord.RequestorRef. Older callers that
    // still pass a SHA hash will find no matches, which is the correct
    // post-rename behaviour — no rows in the new shape were ever hashed.
    Task<int> AnonymiseFairnessByRequestorRefAsync(string tenantId, string requestorRef, CancellationToken cancellationToken = default);
}

public interface IReportingQueryRepository
{
    Task<IReadOnlyList<ParkingMetrics>> QueryMetricsAsync(ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FairnessRecord>> QueryFairnessAsync(FairnessQueryRequest request, string tenantId, CancellationToken cancellationToken = default);
}

public sealed record ReportingQueryRequest
{
    public string? DateFrom { get; init; }
    public string? DateTo { get; init; }
    public string? LocationId { get; init; }
    public string? TimeSlot { get; init; }
}

public sealed record FairnessQueryRequest
{
    public string? DateFrom { get; init; }
    public string? DateTo { get; init; }
    public string? LocationId { get; init; }
}
