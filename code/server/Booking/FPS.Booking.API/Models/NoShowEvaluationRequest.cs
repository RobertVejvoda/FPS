namespace FPS.Booking.API.Models;

public record NoShowEvaluationRequest(
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd,
    string Reason);
