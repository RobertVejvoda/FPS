using FPS.Booking.Application.Models;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Repositories;

public interface IBookingRepository
{
    Task CreateBookingRequestAsync(BookingRequestDto request);
    Task<BookingRequestDto?> GetBookingRequestAsync(string tenantId, Guid requestId);
    Task UpdateBookingRequestStatusAsync(string tenantId, Guid requestId, string status, string? reason = null, string? rejectionCode = null, CancellationToken cancellationToken = default);
    Task UpdateBookingRequestUsageAsync(string tenantId, Guid requestId, string confirmationSource, DateTime confirmedAt, string? sourceEventId = null, CancellationToken cancellationToken = default);
    Task<int> CountRequestsForDateAsync(string tenantId, DateTime date, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingRequestAsync(string tenantId, string requestorId, TimeSlot period, CancellationToken cancellationToken = default);
    Task<bool> HasActiveRequestsForRequestorAsync(string tenantId, string requestorId, CancellationToken cancellationToken = default);
    Task<int> AnonymiseByRequestorIdAsync(string tenantId, string requestorId, CancellationToken cancellationToken = default);
}
