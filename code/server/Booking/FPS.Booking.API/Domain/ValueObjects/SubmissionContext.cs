namespace FPS.Booking.Domain.ValueObjects;

public sealed class SubmissionContext : ValueObject
{
    public int DailyRequestCap { get; }
    public int ExistingRequestCountForDate { get; }
    public bool HasOverlappingRequest { get; }
    public bool IsCutOffPassed { get; }
    public bool IsSameDayRequest { get; }
    public bool SameDayEnabled { get; }
    public bool SameDayCapacityAvailable { get; }

    private SubmissionContext(
        int dailyRequestCap,
        int existingRequestCountForDate,
        bool hasOverlappingRequest,
        bool isCutOffPassed,
        bool isSameDayRequest,
        bool sameDayEnabled,
        bool sameDayCapacityAvailable)
    {
        DailyRequestCap = dailyRequestCap;
        ExistingRequestCountForDate = existingRequestCountForDate;
        HasOverlappingRequest = hasOverlappingRequest;
        IsCutOffPassed = isCutOffPassed;
        IsSameDayRequest = isSameDayRequest;
        SameDayEnabled = sameDayEnabled;
        SameDayCapacityAvailable = sameDayCapacityAvailable;
    }

    // B001 path: future booking
    public static SubmissionContext Create(
        int dailyRequestCap,
        int existingRequestCountForDate,
        bool hasOverlappingRequest,
        bool isCutOffPassed)
    {
        if (dailyRequestCap < 1)
            throw new BookingException("DailyRequestCap must be at least 1");

        return new SubmissionContext(dailyRequestCap, existingRequestCountForDate,
            hasOverlappingRequest, isCutOffPassed,
            isSameDayRequest: false, sameDayEnabled: true, sameDayCapacityAvailable: true);
    }

    // B002 path: same-day booking
    public static SubmissionContext CreateSameDay(
        int dailyRequestCap,
        int existingRequestCountForDate,
        bool hasOverlappingRequest,
        bool sameDayEnabled,
        bool sameDayCapacityAvailable)
    {
        if (dailyRequestCap < 1)
            throw new BookingException("DailyRequestCap must be at least 1");

        return new SubmissionContext(dailyRequestCap, existingRequestCountForDate,
            hasOverlappingRequest, isCutOffPassed: false,
            isSameDayRequest: true, sameDayEnabled, sameDayCapacityAvailable);
    }

    public bool IsCapExceeded => ExistingRequestCountForDate >= DailyRequestCap;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return DailyRequestCap;
        yield return ExistingRequestCountForDate;
        yield return HasOverlappingRequest;
        yield return IsCutOffPassed;
        yield return IsSameDayRequest;
        yield return SameDayEnabled;
        yield return SameDayCapacityAvailable;
    }
}
