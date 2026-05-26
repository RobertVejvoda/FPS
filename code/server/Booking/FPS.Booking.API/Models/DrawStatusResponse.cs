namespace FPS.Booking.API.Models;

public record DrawStatusResponse(
    string DrawKey,
    string Status,
    string? AuditReference,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string DemandLevel);
