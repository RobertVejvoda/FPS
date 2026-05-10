using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Services;

public interface IEmployeeMetricsService
{
    Task<IReadOnlyDictionary<string, EmployeeMetrics>> GetMetricsSnapshotAsync(
        string tenantId,
        IEnumerable<string> requestorIds,
        DateOnly asOfDate,
        int lookbackDays,
        CancellationToken cancellationToken = default);

    Task IncrementRecentAllocationAsync(
        string tenantId,
        string requestorId,
        DateOnly allocationDate,
        CancellationToken cancellationToken = default);
}
