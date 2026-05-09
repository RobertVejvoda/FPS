namespace FPS.Booking.Domain.ValueObjects;

public class DrawDecision : ValueObject
{
    public BookingRequestId RequestId { get; }
    public UserId RequestorId { get; }
    public ParkingSlotId? SlotId { get; }
    public DrawOutcome Outcome { get; }

    private DrawDecision(BookingRequestId requestId, UserId requestorId, ParkingSlotId? slotId, DrawOutcome outcome)
    {
        RequestId = requestId;
        RequestorId = requestorId;
        SlotId = slotId;
        Outcome = outcome;
    }

    public static DrawDecision Allocated(BookingRequestId requestId, UserId requestorId, ParkingSlotId slotId)
        => new(requestId, requestorId, slotId, DrawOutcome.Allocated);

    public static DrawDecision Rejected(BookingRequestId requestId, UserId requestorId)
        => new(requestId, requestorId, null, DrawOutcome.Rejected);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RequestId;
        yield return RequestorId;
        yield return SlotId ?? (object)"null";
        yield return Outcome;
    }
}
