using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Repositories;

public interface IBookingQueryRepository
{
    Task<BookingListResult> GetByRequestorAsync(
        string tenantId,
        string requestorId,
        DateOnly from,
        DateOnly? to,
        string? statusFilter,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task AddToUserIndexAsync(
        string tenantId,
        string requestorId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task AddToTenantPendingIndexAsync(
        string tenantId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task AddToTenantOpsIndexAsync(
        string tenantId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<HrBookingListResult> GetByTenantAsync(
        string tenantId,
        string? locationId,
        DateOnly? from,
        DateOnly? to,
        string? statusFilter,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<HrEmployeeHistoryResult> GetEmployeeHistoryAsync(
        string tenantId,
        string requestorId,
        DateOnly? from,
        DateOnly? to,
        string? statusFilter,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<HrSlotHistoryResult> GetSlotHistoryAsync(
        string tenantId,
        string? locationId,
        string slotId,
        DateOnly? from,
        DateOnly? to,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookingRequestDto>> GetAllocatedRequestsForDrawAsync(
        string tenantId,
        string locationId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookingRequestDto>> GetPendingRequestsForDrawAsync(
        string tenantId,
        string locationId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Destructive tenant purge (PLAT003C): removes every booking request for the tenant plus
    /// its derived per-entity keys (penalties, fairness metrics, reconstructed draw attempts,
    /// per-date counters) and the tenant-wide ops/pending/user indexes. Idempotent — a second
    /// run over an already-empty tenant returns 0 and throws nothing. Returns the number of
    /// booking requests removed.
    /// </summary>
    Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
