namespace FPS.Booking.Application.Models;

/// <summary>
/// Represents a single action step in the Draw lifecycle.
/// Captures business-readable step name, result status, and correlation metadata
/// for auditor/admin transparency.
/// </summary>
public class DrawLifecycleStepDto
{
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Skipped, Failed
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ReasonCode { get; set; }
    public string? Summary { get; set; }
    public string? TraceId { get; set; }
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Extended Draw lifecycle view for auditors and authorized administrators.
/// Includes step-level tracking, per-booking decisions, and deterministic evidence.
/// </summary>
public class DrawLifecycleResult
{
    public string DrawKey { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Failed, Partial
    public long Seed { get; set; }
    public string AlgorithmVersion { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Lifecycle steps with business-readable names
    public List<DrawLifecycleStepDto> Steps { get; set; } = [];

    // Summary counts
    public int AllocatedCount { get; set; }
    public int RejectedCount { get; set; }
    public int WaitlistedCount { get; set; }

    // Per-booking decisions linked to this Draw
    public List<DrawDecisionDto> Decisions { get; set; } = [];

    // Deterministic evidence for auditor fairness verification
    public List<string> Tier2CandidateSequence { get; set; } = [];

    // Correlation metadata
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
}
