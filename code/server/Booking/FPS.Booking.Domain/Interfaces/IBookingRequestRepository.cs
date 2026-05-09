using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;

namespace FPS.Booking.Domain.Interfaces;

public interface IBookingRequestRepository
{
    Task<BookingRequest> GetByIdAsync(BookingRequestId id);
    Task<IEnumerable<BookingRequest>> GetByUserIdAsync(UserId userId);
    Task<IEnumerable<BookingRequest>> GetPendingRequestsAsync();
    Task<IEnumerable<BookingRequest>> GetRequestsForPeriodAsync(TimeSlot period);
    Task AddAsync(BookingRequest bookingRequest);
    Task UpdateAsync(BookingRequest bookingRequest);
}