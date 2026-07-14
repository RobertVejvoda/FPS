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
    string? CannotRequestReason = null,
    // LOC002 (#799): stable machine codes alongside the free-text fields so
    // clients localize by code and only fall back to the English text.
    string ScheduleMessageCode = "",
    string? CannotRequestCode = null);
