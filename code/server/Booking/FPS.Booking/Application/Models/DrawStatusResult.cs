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
    string? CannotRequestReason = null,
    // LOC002 (#799): stable machine codes mirroring SafeMessage / CannotRequestReason
    // so clients localize by code instead of matching English text.
    string ScheduleMessageCode = "",
    string? CannotRequestCode = null);

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

// LOC002 (#799): stable codes for the employee-safe schedule banner message.
// Clients localize by code; SafeMessage stays as the English fallback.
public static class ScheduleMessageCode
{
    public const string NotConfigured = "schedule.notConfigured";
    public const string WindowClosed = "schedule.windowClosed";
    public const string OpenUntil = "schedule.openUntil";
    public const string AllocationComplete = "schedule.allocationComplete";
}

// LOC002 (#799): stable codes for why a request is blocked. Clients localize by
// code; CannotRequestReason stays as the English fallback.
public static class CannotRequestCode
{
    public const string DatePassed = "request.datePassed";
    public const string AllocationComplete = "request.allocationComplete";
    public const string DrawInProgress = "request.drawInProgress";
    public const string WindowClosed = "request.windowClosed";
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
