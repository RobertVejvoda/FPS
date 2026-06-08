namespace FPS.Booking.Application.Models;

public record HrDrawOutcomeSummary(
    string Date,
    string TimeSlot,
    string? LocationId,
    string DrawStatus,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    int TotalRequests,
    DateTime? CompletedAt,
    IReadOnlyList<HrDrawOutcomeItem> Outcomes);

public record HrDrawOutcomeItem(
    Guid RequestId,
    string RequestorRef,
    string Outcome,
    string? ReasonCode,
    string? Reason,
    string? AllocatedSlotId);
