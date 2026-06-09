namespace FPS.Booking.Application.Models;

public record ManualCorrectionResult(
    Guid RequestId,
    string CorrectionType,
    string NewValue,
    DateTime AppliedAt);
