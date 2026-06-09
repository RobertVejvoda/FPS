namespace FPS.Booking.Models;

public record DrawStatusResponse(
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string DemandLevel,
    // Schedule metadata (DRAW005)
    string? CutOffAt,
    string? NextDrawAt,
    string TimeZone,
    string RequestWindowStatus,
    string ScheduleStatus,
    string ScheduleSource,
    DateTime LastCalculatedAt,
    string SafeMessage,
    int RequestCount = 0,
    int AvailableSpotCount = 0,
    bool CanRequest = true,
    string? CannotRequestReason = null);
