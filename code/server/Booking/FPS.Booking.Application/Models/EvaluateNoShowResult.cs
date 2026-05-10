namespace FPS.Booking.Application.Models;

public record EvaluateNoShowResult(
    int MarkedCount,
    int SkippedCount,
    string? SkippedReason);
