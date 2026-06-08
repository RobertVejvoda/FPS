namespace FPS.Booking.Application.Models;

public record MyDrawOutcomeSummary(
    string Date,
    string TimeSlot,
    string? LocationId,
    string DrawStatus,
    int AllocatedCount,
    int TotalRequests,
    DateTime? CompletedAt,
    string MyOutcome,
    string? MyReason,
    string? MyAllocatedSlotId);
