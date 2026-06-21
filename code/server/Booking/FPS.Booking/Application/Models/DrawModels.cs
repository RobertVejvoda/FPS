namespace FPS.Booking.Application.Models;

public class DrawAttemptDto
{
    public string DrawKey { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "Pending";
    public long Seed { get; set; }
    public string AlgorithmVersion { get; set; } = string.Empty;
    public int AllocatedCount { get; set; }
    public int RejectedCount { get; set; }
    public int WaitlistedCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<DrawDecisionDto> Decisions { get; set; } = [];
    public List<string> Tier2CandidateSequence { get; set; } = [];
    public List<DrawLifecycleStepRecord> LifecycleSteps { get; set; } = [];

    /// <summary>
    /// ETag for optimistic concurrency control. Dapr state stores supporting ETags
    /// will use this for compare-and-set semantics. For stores without ETag support,
    /// updates use last-write-wins.
    /// </summary>
    public string? ETag { get; set; }
}

public class DrawLifecycleStepRecord
{
    public string StepName { get; set; } = string.Empty;
    // Completed | Attempted | Failed
    public string Status { get; set; } = "Completed";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Summary { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DrawDecisionDto
{
    public string RequestId { get; set; } = string.Empty;
    public string RequestorId { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? SlotId { get; set; }
    public string? Reason { get; set; }

    /// <summary>
    /// Allocated parking slot reference visible to employee and HR views.
    /// Populated when Outcome is "Allocated". Used for allocation explanations
    /// and HR operational views, not just internal Draw decisions.
    /// </summary>
    public string? AllocatedSlotReference { get; set; }

    /// <summary>
    /// True when this allocation was a guaranteed Tier 1 company-car fixed-slot win.
    /// False for all Tier 2 lottery wins, including company-car fallbacks without an assigned fixed slot.
    /// Used by PersistDecisionsActivity to skip fairness-metric increments only for genuine Tier 1 allocations.
    /// </summary>
    public bool IsTier1Guaranteed { get; set; }
}

public record TriggerDrawResult(
    string DrawAttemptId,
    string Status,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    bool WasAlreadyCompleted);
