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
}
