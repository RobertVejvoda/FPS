namespace FPS.Configuration.Domain;

public sealed record ParkingPolicy
{
    public string TenantId { get; init; } = string.Empty;
    public string? LocationId { get; init; }
    public string TimeZone { get; init; } = string.Empty;
    public TimeOnly DrawCutOffTime { get; init; }
    public int DailyRequestCap { get; init; }
    public int AllocationLookbackDays { get; init; }
    public int LateCancellationPenalty { get; init; }
    public int NoShowPenalty { get; init; }
    public bool ManualAdjustmentEnabled { get; init; }
    public bool SameDayBookingEnabled { get; init; }
    public bool SameDayUsesRequestCap { get; init; }
    public bool AutomaticReallocationEnabled { get; init; }
    public bool UsageConfirmationRequired { get; init; }
    public int UsageConfirmationWindowMinutes { get; init; }
    public IReadOnlyList<string> UsageConfirmationMethods { get; init; } = [];
    public bool NoShowDetectionEnabled { get; init; }
    public bool CompanyCarTier1Enabled { get; init; }
    public string CompanyCarOverflowBehavior { get; init; } = "reject";
    public string PublishedByUserId { get; init; } = string.Empty;
    public DateTimeOffset PublishedAt { get; init; }
    public string? PublicationReason { get; init; }

    internal const int V1DailyRequestCapLimit = 500;
}
