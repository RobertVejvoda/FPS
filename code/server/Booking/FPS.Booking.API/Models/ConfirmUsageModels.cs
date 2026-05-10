namespace FPS.Booking.API.Models;

public record ConfirmUsageRequest(
    string ConfirmationSource,
    DateTime? ConfirmedAt = null,
    string? SourceEventId = null,
    string? Reason = null);

public record ConfirmUsageResponse(
    Guid RequestId,
    string Status,
    DateTime ConfirmedAt,
    bool WasAlreadyConfirmed);
