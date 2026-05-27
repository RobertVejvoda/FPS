namespace FPS.Booking.API.Models;

public record DrawStatusResponse(
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string DemandLevel,
    int RequestCount = 0,
    bool CanRequest = true,
    string? CannotRequestReason = null);
