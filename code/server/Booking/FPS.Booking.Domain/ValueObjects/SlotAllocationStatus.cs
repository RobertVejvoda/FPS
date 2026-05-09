namespace FPS.Booking.Domain.ValueObjects;

public enum SlotAllocationStatus
{
    Reserved,    // Slot is initially reserved 
    InUse,       // User has checked in and is using the slot
    Completed,   // User has completed parking and checked out
    Cancelled    // Allocation was cancelled
}