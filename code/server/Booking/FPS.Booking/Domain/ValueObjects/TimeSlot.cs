namespace FPS.Booking.Domain.ValueObjects;

public class TimeSlot : ValueObject
{
    public DateTime Start { get; }
    public DateTime End { get; }
    public TimeSpan Duration => End - Start;

    private TimeSlot(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    public static TimeSlot Create(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new BookingException("End time must be after start time");

        return new TimeSlot(start, end);
    }

    public static TimeSlot CreateFromDuration(DateTime start, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            throw new BookingException("Duration must be positive");

        return Create(start, start.Add(duration));
    }

    public bool Overlaps(TimeSlot other)
    {
        return Start < other.End && End > other.Start;
    }

    public bool Contains(DateTime time)
    {
        return time >= Start && time <= End;
    }

    public bool IsWithinBusinessHours()
    {
        // Example business rule: bookings can only be between 6am and 11pm
        return Start.Hour >= 6 && End.Hour <= 23;
    }

    public bool IsFutureTimeSlot()
    {
        return Start > DateTime.Now;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}