namespace FPS.Booking.Models;

public record NoShowEvaluationRequest(
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd,
    string Reason);
