namespace FPS.Booking.Application.Services;

public sealed class DrawSchedulerOptions
{
    public const string SectionName = "DrawScheduler";

    public bool Enabled { get; set; } = true;

    // How many days ahead from the trigger date to compute the target parking date.
    // Default 1 = "tomorrow's draw runs tonight".
    public int TargetDateOffsetDays { get; set; } = 1;

    // The UTC time of day at which the draw is considered "due". Defaults to 18:00.
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
