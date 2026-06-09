namespace FPS.Booking.Domain.ValueObjects;

public class UserId : StronglyTypedId<Guid>
{
    private UserId(Guid value) : base(value) { }

    public static UserId New() => new(Guid.NewGuid());

    public static UserId FromGuid(Guid id)
    {
        if (id == Guid.Empty)
            throw new BookingException("UserId cannot be empty");

        return new UserId(id);
    }

    public static UserId FromString(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new BookingException("UserId cannot be empty");

        return new UserId(Guid.Parse(id));
    }
}