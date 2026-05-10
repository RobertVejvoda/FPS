namespace FPS.Booking.Domain.ValueObjects;

public enum BookingRejectionCode
{
    PastDate,
    CutOffPassed,
    DailyCapExceeded,
    DuplicateRequest,
    VehicleConstraintUnmatched,
    NoCapacityAvailable,
    RequestorIneligible,
    SameDayBookingDisabled,
    NoCapacityForSameDay
}
