using FPS.SharedKernel.Interfaces;

namespace FPS.Booking.Domain.Entities;

public sealed class Penalty : IEntity<Guid>
{
    public Guid Id { get; private set; }
    public BookingRequestId RequestId { get; private set; }
    public UserId RequestorId { get; private set; }
    public PenaltyType Type { get; private set; }
    public int Score { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public string SourceEventId { get; private set; }

    private Penalty() { }

    public static Penalty Create(
        BookingRequestId requestId,
        UserId requestorId,
        PenaltyType type,
        int score,
        DateOnly effectiveDate,
        int expiryDays,
        string sourceEventId)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(requestorId);
        if (score < 1) throw new BookingException("Penalty score must be positive");
        if (string.IsNullOrWhiteSpace(sourceEventId)) throw new BookingException("SourceEventId is required for idempotency");

        return new Penalty
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            RequestorId = requestorId,
            Type = type,
            Score = score,
            EffectiveDate = effectiveDate,
            ExpiryDate = effectiveDate.AddDays(expiryDays),
            SourceEventId = sourceEventId
        };
    }

    public bool IsActiveOn(DateOnly date) => date >= EffectiveDate && date <= ExpiryDate;
}
