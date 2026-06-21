namespace FPS.Booking.Domain.ValueObjects;

public sealed class DrawDecision : ValueObject
{
    public BookingRequestId RequestId { get; }
    public UserId RequestorId { get; }
    public DrawOutcome Outcome { get; }
    public ParkingSlotId? SlotId { get; }
    public string? Reason { get; }

    // True only for company-car requests whose HR-assigned fixed slot was found and consumed in Tier 1.
    // False for all Tier 2 lottery wins, including company-car fallbacks without an assigned fixed slot.
    public bool IsTier1Guaranteed { get; }

    private DrawDecision(BookingRequestId requestId, UserId requestorId, DrawOutcome outcome, ParkingSlotId? slotId, string? reason, bool isTier1Guaranteed = false)
    {
        RequestId = requestId;
        RequestorId = requestorId;
        Outcome = outcome;
        SlotId = slotId;
        Reason = reason;
        IsTier1Guaranteed = isTier1Guaranteed;
    }

    public static DrawDecision Allocated(BookingRequestId requestId, UserId requestorId, ParkingSlotId slotId)
        => new(requestId, requestorId, DrawOutcome.Allocated, slotId, null);

    public static DrawDecision AllocatedTier1Guaranteed(BookingRequestId requestId, UserId requestorId, ParkingSlotId slotId)
        => new(requestId, requestorId, DrawOutcome.Allocated, slotId, null, isTier1Guaranteed: true);

    public static DrawDecision Rejected(BookingRequestId requestId, UserId requestorId, string reason)
        => new(requestId, requestorId, DrawOutcome.Rejected, null, reason);

    public static DrawDecision Waitlisted(BookingRequestId requestId, UserId requestorId)
        => new(requestId, requestorId, DrawOutcome.Waitlisted, null, "Capacity exhausted — waiting for released slot.");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RequestId;
        yield return Outcome;
        yield return SlotId ?? (object)"null";
        yield return IsTier1Guaranteed;
    }
}
