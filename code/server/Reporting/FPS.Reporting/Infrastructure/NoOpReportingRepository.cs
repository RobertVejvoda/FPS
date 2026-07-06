using FPS.Reporting.Domain;

namespace FPS.Reporting.Infrastructure;

/// <summary>
/// #763 — the production write-side repository. In Production/NAS, DataHub owns the durable report
/// projections, so Reporting must not keep its own in-memory tenant state (which would be lost on
/// restart and is no longer the source of truth). This no-op absorbs the legacy <c>booking-events</c>
/// projection path without storing anything, so no core tenant-scoped state lives in process memory.
/// Reads are served by <see cref="DataHubReportingQueryRepository"/>. Purge/erasure are honestly
/// inert here — the durable report data lives in DataHub and is removed by DataHub tenant purge
/// (user-level reporting erasure in DataHub is a follow-up).
/// </summary>
public sealed class NoOpReportingRepository : IReportingRepository
{
    public Task<bool> EventExistsAsync(string eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task RecordEventIdAsync(string tenantId, string eventId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ApplyMetricsAsync(string tenantId, string date, string locationId, string timeSlot,
        Action<ParkingMetrics> apply, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ApplyFairnessAsync(string tenantId, string requestorRef, string date, string locationId,
        Action<FairnessRecord> apply, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    // No in-memory state to purge — DataHub tenant purge removes the durable report data.
    public Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    // No in-memory fairness rows to anonymise — DataHub owns the durable data (follow-up: user-level
    // reporting erasure in DataHub). This does NOT anonymise DataHub-backed reports.
    public Task<int> AnonymiseFairnessByRequestorRefAsync(string tenantId, string requestorRef, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
