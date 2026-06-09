namespace FPS.Booking.Models;

public record ManualCorrectionRequest(
    string CorrectionType,
    string OldValue,
    string NewValue,
    string Reason,
    DateTime? EffectiveAt = null);
