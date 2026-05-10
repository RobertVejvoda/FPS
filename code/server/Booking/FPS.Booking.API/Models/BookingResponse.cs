using FPS.Booking.Application.Models;

namespace FPS.Booking.API.Models;

public record SubmitBookingResponse(
    Guid RequestId,
    string Status,
    string? RejectionCode,
    string? Reason);

public record GetMyBookingsResponse(
    IReadOnlyList<BookingListItem> Items,
    string? NextCursor);
