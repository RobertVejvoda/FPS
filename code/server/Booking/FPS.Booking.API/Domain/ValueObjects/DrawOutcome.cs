namespace FPS.Booking.Domain.ValueObjects;

public enum DrawOutcome
{
    Allocated,   // Won a slot
    Rejected,    // Hard rejection — invalid, duplicate, overflow, ineligible
    Waitlisted   // Eligible but capacity exhausted — remains Pending for reallocation
}
