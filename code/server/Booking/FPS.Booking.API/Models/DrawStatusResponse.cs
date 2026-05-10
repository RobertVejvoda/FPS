namespace FPS.Booking.API.Models;

public record DrawStatusResponse(
    string DrawKey,
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
    DateTime? CompletedAt);
