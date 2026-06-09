namespace FPS.Booking.Domain.ValueObjects;

public sealed class DrawDecision : ValueObject
{
    public BookingRequestId RequestId { get; }
    public UserId RequestorId { get; }
    public DrawOutcome Outcome { get; }
    public ParkingSlotId? SlotId { get; }
    public string? Reason { get; }

    private DrawDecision(BookingRequestId requestId, UserId requestorId, DrawOutcome outcome, ParkingSlotId? slotId, string? reason)
    {
        RequestId = requestId;
        RequestorId = requestorId;
        Outcome = outcome;
        SlotId = slotId;
        Reason = reason;
    }

    public static DrawDecision Allocated(BookingRequestId requestId, UserId requestorId, ParkingSlotId slotId)
        => new(requestId, requestorId, DrawOutcome.Allocated, slotId, null);

    public static DrawDecision Rejected(BookingRequestId requestId, UserId requestorId, string reason)
        => new(requestId, requestorId, DrawOutcome.Rejected, null, reason);

    public static DrawDecision Waitlisted(BookingRequestId requestId, UserId requestorId)
        => new(requestId, requestorId, DrawOutcome.Waitlisted, null, "Capacity exhausted — waiting for released slot.");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RequestId;
        yield return Outcome;
        yield return SlotId ?? (object)"null";
    }
}
