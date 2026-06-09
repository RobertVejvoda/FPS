namespace FPS.Booking.Domain.ValueObjects;

public enum BookingRequestStatus
{
    Submitted,   // Received, pending initial validation
    Pending,     // Validation passed, waiting for Draw or same-day allocation
    Allocated,   // A parking slot has been assigned
    Rejected,    // Cannot be fulfilled — terminal
    Cancelled,   // Cancelled by requestor, HR, or system — terminal
    Used,        // Confirmed as used — terminal
    NoShow,      // Not confirmed within policy window — terminal
    Expired      // Time window passed without a final outcome — terminal
}
