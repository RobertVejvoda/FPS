namespace FPS.DataHub.Domain;

/// <summary>
/// Draw history projection for HR operational views and reporting.
/// Populated from booking.drawStarted and booking.drawCompleted events.
/// </summary>
public sealed class DrawHistoryProjection
{
    /// <summary>Primary key</summary>
    public long Id { get; set; }

    /// <summary>Source draw attempt event ID from Booking</summary>
    public string DrawAttemptId { get; set; } = "";

    /// <summary>Tenant owning this Draw</summary>
    public string TenantId { get; set; } = "";

    /// <summary>Location where Draw ran</summary>
    public string LocationId { get; set; } = "";

    /// <summary>Parking date targeted by the Draw</summary>
    public DateOnly Date { get; set; }

    /// <summary>Time slot targeted (HH:mm-HH:mm format)</summary>
    public string TimeSlot { get; set; } = "";

    /// <summary>Draw status: Scheduled, Running, Completed, Failed</summary>
    public string Status { get; set; } = "Scheduled";

    /// <summary>Trigger source: scheduled, manual, simulation</summary>
    public string? TriggerSource { get; set; }

    /// <summary>When the Draw started</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When the Draw completed or failed</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Number of requests allocated</summary>
    public int AllocatedCount { get; set; }

    /// <summary>Number of requests rejected</summary>
    public int RejectedCount { get; set; }

    /// <summary>Number of requests waitlisted</summary>
    public int WaitlistedCount { get; set; }

    /// <summary>Safe failure reason when status is Failed</summary>
    public string? SafeFailureReason { get; set; }

    /// <summary>Algorithm version used</summary>
    public string? AlgorithmVersion { get; set; }

    /// <summary>Last updated timestamp for projection freshness</summary>
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
