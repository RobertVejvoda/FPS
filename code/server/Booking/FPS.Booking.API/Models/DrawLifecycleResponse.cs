namespace FPS.Booking.API.Models;

public record DrawLifecycleResponse(
    string DrawKey,
    string LocationId,
    string Date,
    string Status,
    string AlgorithmVersion,
    long? Seed,
    string? AuditReference,
    int RequestCount,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<DrawLifecycleStepResponse> Steps,
    IReadOnlyList<DrawLifecycleDecisionResponse> Decisions,
    IReadOnlyList<string> Tier2CandidateSequence);

public record DrawLifecycleStepResponse(
    string Name,
    string Status,
    string? Summary,
    DateTime? OccurredAt,
    string? ErrorMessage = null);

public record DrawLifecycleDecisionResponse(
    string BookingReference,
    string Outcome,
    string? SlotReference,
    string? Reason);
