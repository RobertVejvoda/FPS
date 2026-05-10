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
}

public class DrawDecisionDto
{
    public string RequestId { get; set; } = string.Empty;
    public string RequestorId { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? SlotId { get; set; }
    public string? Reason { get; set; }
}

public record TriggerDrawResult(
    string DrawAttemptId,
    string Status,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    bool WasAlreadyCompleted);
