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
    DateTime? CompletedAt);
