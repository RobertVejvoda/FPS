namespace FPS.Booking.Application.Models;

public record DrawLifecycleResult(
    string DrawKey,
    string LocationId,
    DateOnly Date,
    string Status,
    string AlgorithmVersion,
    long Seed,
    string AuditReference,
    int RequestCount,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<DrawLifecycleStep> Steps,
    IReadOnlyList<DrawLifecycleDecision> Decisions,
    IReadOnlyList<string> Tier2CandidateSequence);

public record DrawLifecycleStep(
    string Name,
    string Status,
    string? Summary,
    DateTime? OccurredAt,
    string? ErrorMessage = null);

public record DrawLifecycleDecision(
    string BookingReference,
    string Outcome,
    string? SlotReference,
    string? Reason);
