namespace FPS.Booking.Application.Services;

public sealed class DrawSchedulerOptions
{
    public const string SectionName = "DrawScheduler";

    public bool Enabled { get; set; } = true;

    // How many days ahead from the trigger date to compute the target parking date.
    // Default 1 = "tomorrow's draw runs tonight".
    public int TargetDateOffsetDays { get; set; } = 1;

    // The IANA/Windows timezone ID used to interpret DrawCutOffTime. Defaults to UTC.
    public string PolicyTimeZoneId { get; set; } = "UTC";

    // The local time of day (in PolicyTimeZoneId) at which the draw is considered "due". Defaults to 18:00.
    public TimeSpan DrawCutOffTime { get; set; } = TimeSpan.FromHours(18);

    public List<DrawScheduleTarget> Targets { get; set; } = [];
}

public sealed class DrawScheduleTarget
{
    public string TenantId { get; set; } = "";
    public string LocationId { get; set; } = "";

    // Stored as TimeSpan strings (e.g. "09:00:00") in appsettings.
    public TimeSpan TimeSlotStart { get; set; }
    public TimeSpan TimeSlotEnd { get; set; }
}
