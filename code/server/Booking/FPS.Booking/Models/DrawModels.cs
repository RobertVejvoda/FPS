namespace FPS.Booking.Models;

public record TriggerDrawRequest(
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd,
    string Reason,
    bool AllowRecovery = false);

public record TriggerDrawResponse(
    string DrawAttemptId,
    string Status,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount);

public record HrDrawOutcomesResponse(IReadOnlyList<HrDrawOutcomeSummaryResponse> Draws);

public record HrDrawOutcomeSummaryResponse(
    string Date,
    string TimeSlot,
    string? LocationId,
    string DrawStatus,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    int TotalRequests,
    DateTime? CompletedAt,
    IReadOnlyList<HrDrawOutcomeItemResponse> Outcomes);

public record HrDrawOutcomeItemResponse(
    Guid RequestId,
    string RequestorRef,
    string Outcome,
    string? ReasonCode,
    string? Reason,
    string? AllocatedSlotId);

public record MyDrawOutcomesResponse(IReadOnlyList<MyDrawOutcomeSummaryResponse> Draws);

public record MyDrawOutcomeSummaryResponse(
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
