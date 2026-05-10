using FPS.Booking.Application.Models;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Repositories;

public interface IBookingRepository
{
    Task CreateBookingRequestAsync(BookingRequestDto request);
    Task<BookingRequestDto?> GetBookingRequestAsync(Guid requestId);
    Task UpdateBookingRequestStatusAsync(Guid requestId, string status, string? reason = null, CancellationToken cancellationToken = default);
    Task UpdateBookingRequestUsageAsync(Guid requestId, string confirmationSource, DateTime confirmedAt, string? sourceEventId = null, CancellationToken cancellationToken = default);
    Task<int> CountRequestsForDateAsync(string tenantId, DateTime date, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingRequestAsync(string tenantId, string requestorId, TimeSlot period, CancellationToken cancellationToken = default);
}
