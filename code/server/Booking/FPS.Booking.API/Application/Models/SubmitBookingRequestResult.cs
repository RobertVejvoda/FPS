namespace FPS.Booking.Application.Models;

public record SubmitBookingRequestResult(
    Guid RequestId,
    string Status,
    string? RejectionCode,
    string? Reason);
