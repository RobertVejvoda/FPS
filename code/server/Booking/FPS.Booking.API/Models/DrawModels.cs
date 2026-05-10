namespace FPS.Booking.API.Models;

public record TriggerDrawRequest(
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd,
    string Reason);

public record TriggerDrawResponse(
    string DrawAttemptId,
    string Status,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount);
