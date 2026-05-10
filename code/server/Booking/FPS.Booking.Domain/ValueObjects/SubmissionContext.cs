namespace FPS.Booking.Domain.ValueObjects;

public class SubmissionContext : ValueObject
{
    public int DailyRequestCap { get; }
    public int ExistingRequestCountForDate { get; }
    public bool HasOverlappingRequest { get; }
    public bool IsCutOffPassed { get; }

    private SubmissionContext(
        int dailyRequestCap,
        int existingRequestCountForDate,
        bool hasOverlappingRequest,
        bool isCutOffPassed)
    {
        DailyRequestCap = dailyRequestCap;
        ExistingRequestCountForDate = existingRequestCountForDate;
        HasOverlappingRequest = hasOverlappingRequest;
        IsCutOffPassed = isCutOffPassed;
    }

    public static SubmissionContext Create(
        int dailyRequestCap,
        int existingRequestCountForDate,
        bool hasOverlappingRequest,
        bool isCutOffPassed)
    {
        if (dailyRequestCap < 1)
            throw new BookingException("DailyRequestCap must be at least 1");

        return new SubmissionContext(dailyRequestCap, existingRequestCountForDate, hasOverlappingRequest, isCutOffPassed);
    }

    public bool IsCapExceeded => ExistingRequestCountForDate >= DailyRequestCap;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return DailyRequestCap;
        yield return ExistingRequestCountForDate;
        yield return HasOverlappingRequest;
        yield return IsCutOffPassed;
    }
}
