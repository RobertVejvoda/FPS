namespace FPS.Booking.Domain.ValueObjects;

public sealed class DrawKey : ValueObject
{
    public string TenantId { get; }
    public string LocationId { get; }
    public DateOnly Date { get; }
    public TimeSlot TimeSlot { get; }

    private DrawKey(string tenantId, string locationId, DateOnly date, TimeSlot timeSlot)
    {
        TenantId = tenantId;
        LocationId = locationId;
        Date = date;
        TimeSlot = timeSlot;
    }

    public static DrawKey Create(string tenantId, string locationId, DateOnly date, TimeSlot timeSlot)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) throw new BookingException("TenantId is required for DrawKey");
        if (string.IsNullOrWhiteSpace(locationId)) throw new BookingException("LocationId is required for DrawKey");
        ArgumentNullException.ThrowIfNull(timeSlot);

        return new DrawKey(tenantId, locationId, date, timeSlot);
    }

    public string ToStoreKey() => $"draw:{TenantId}:{LocationId}:{Date:yyyy-MM-dd}:{TimeSlot.Start:HHmm}";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TenantId;
        yield return LocationId;
        yield return Date;
        yield return TimeSlot;
    }
}
