namespace FPS.Booking.Domain.ValueObjects;

public enum BookingRequestStatus
{
    Pending,      // Initial state when a request is submitted
    Accepted,     // Request is accepted and slot allocation is created
    Rejected,     // Request is rejected (e.g., no slots available)
    Cancelled     // Request was cancelled by user or system
}