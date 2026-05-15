namespace FPS.Reporting.Domain;

public interface IReportingRepository
{
    Task<bool> EventExistsAsync(string eventId, CancellationToken cancellationToken = default);
    Task RecordEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task ApplyMetricsAsync(string tenantId, string date, string locationId, string timeSlot,
        Action<ParkingMetrics> apply, CancellationToken cancellationToken = default);
    Task ApplyFairnessAsync(string tenantId, string requestorHash,
        Action<FairnessRecord> apply, CancellationToken cancellationToken = default);
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
