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
    int AvailableSpotCount,
    DateTime? NextDrawAt,
    bool CanRequest,
    string? CannotRequestReason);

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
