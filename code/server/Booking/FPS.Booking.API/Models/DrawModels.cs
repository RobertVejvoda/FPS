namespace FPS.Booking.API.Models;

public record TriggerDrawRequest(
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd,
    string Reason);

public record TriggerDrawResponse(
    string DrawAttemptId,
    string Status,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount);

/// <summary>
/// Lifecycle step in the Draw process for auditor/admin visibility.
/// </summary>
public record DrawLifecycleStepResponse(
    string StepName,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ReasonCode,
    string? Summary);

/// <summary>
/// Per-booking decision in the Draw for auditor/admin visibility.
/// </summary>
public record DrawDecisionResponse(
    string RequestId,
    string RequestorId,
    string Outcome,
    string? SlotId,
    string? Reason);

/// <summary>
/// Full Draw lifecycle response for auditor/admin use.
/// Exposes step-level tracking, decisions, and deterministic fairness evidence.
/// </summary>
public record DrawLifecycleResponse(
    string DrawKey,
    string TenantId,
    string LocationId,
    DateOnly Date,
    string Status,
    long Seed,
    string AlgorithmVersion,
    DateTime StartedAt,
    DateTime? CompletedAt,
    List<DrawLifecycleStepResponse> Steps,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    List<DrawDecisionResponse> Decisions,
    List<string> Tier2CandidateSequence,
    string? CorrelationId,
    string? TraceId);
