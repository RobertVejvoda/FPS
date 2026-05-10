namespace FPS.Booking.Application.Models;

public record ConfirmSlotUsageResult(
    Guid RequestId,
    string Status,
    DateTime ConfirmedAt,
    bool WasAlreadyConfirmed);
