namespace FPS.Booking.Application.Models;

public record DrawStatusResult(
    string DrawKey,
    string TenantId,
    string LocationId,
    DateOnly Date,
    string Status,
    int RequestCount,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    int CompanyCarOverflowCount,
    IReadOnlyList<string> SummaryRejectionReasons,
    string AlgorithmVersion,
    long Seed,
    string? AuditReference,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string DemandLevel,
    // Schedule metadata (DRAW005)
    string? CutOffAt,
    string? NextDrawAt,
    string TimeZone,
    string RequestWindowStatus,
    string ScheduleStatus,
    string ScheduleSource,
    DateTime LastCalculatedAt,
    string SafeMessage,
    int AvailableSpotCount = 0,
    bool CanRequest = true,
    string? CannotRequestReason = null);

public static class RequestWindowStatus
{
    public const string Open = "open";
    public const string Closed = "closed";
    public const string Unknown = "unknown";
}

public static class ScheduleStatus
{
    public const string Known = "known";
    public const string NotConfigured = "notConfigured";
    public const string Disabled = "disabled";
    public const string Unknown = "unknown";
}

public static class ScheduleSource
{
    public const string TenantPolicy = "tenantPolicy";
    public const string LocationOverride = "locationOverride";
    public const string ManualOnly = "manualOnly";
}

public static class DemandLevel
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Unknown = "Unknown";

    public static string FromOutcomes(int requestCount, int allocatedCount)
    {
        if (requestCount == 0) return Unknown;
        var satisfactionRate = (double)allocatedCount / requestCount;
        return satisfactionRate switch
        {
            >= 0.9 => Low,
            >= 0.6 => Medium,
            _ => High
        };
    }
}
