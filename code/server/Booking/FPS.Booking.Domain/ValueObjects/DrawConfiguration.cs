namespace FPS.Booking.Domain.ValueObjects;

public class DrawConfiguration : ValueObject
{
    public int MaxFailedDrawCap { get; }
    public int GuaranteedWinThreshold { get; }

    private DrawConfiguration(int maxFailedDrawCap, int guaranteedWinThreshold)
    {
        MaxFailedDrawCap = maxFailedDrawCap;
        GuaranteedWinThreshold = guaranteedWinThreshold;
    }

    public static DrawConfiguration Default() => new(maxFailedDrawCap: 10, guaranteedWinThreshold: 15);

    public static DrawConfiguration Create(int maxFailedDrawCap, int guaranteedWinThreshold)
    {
        if (maxFailedDrawCap < 1)
            throw new BookingException("MaxFailedDrawCap must be at least 1");
        if (guaranteedWinThreshold < 1)
            throw new BookingException("GuaranteedWinThreshold must be at least 1");

        return new DrawConfiguration(maxFailedDrawCap, guaranteedWinThreshold);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return MaxFailedDrawCap;
        yield return GuaranteedWinThreshold;
    }
}
