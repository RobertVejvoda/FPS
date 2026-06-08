namespace FPS.DataHub.Domain;

/// <summary>
/// Booking outcome projection for employee Past Draw Outcomes and HR operational views.
/// Populated from booking request lifecycle events.
/// </summary>
public sealed class BookingOutcomeProjection
{
    /// <summary>Primary key</summary>
    public long Id { get; set; }

    /// <summary>Booking request ID from Booking service</summary>
    public string BookingRequestId { get; set; } = "";

    /// <summary>Tenant owning this booking</summary>
    public string TenantId { get; set; } = "";

    /// <summary>Employee/requestor reference</summary>
    public string RequestorId { get; set; } = "";

    /// <summary>Location requested</summary>
    public string LocationId { get; set; } = "";

    /// <summary>Parking date requested</summary>
    public DateOnly Date { get; set; }

    /// <summary>Time slot requested (HH:mm-HH:mm format)</summary>
    public string TimeSlot { get; set; } = "";

    /// <summary>Final status: Submitted, Allocated, Rejected, Cancelled, Used, NoShow, Expired</summary>
    public string FinalStatus { get; set; } = "Submitted";

    /// <summary>Safe reason code for rejection/cancellation</summary>
    public string? ReasonCode { get; set; }

    /// <summary>Employee-visible reason text</summary>
    public string? SafeReasonText { get; set; }

    /// <summary>Allocation ID when allocated</summary>
    public string? AllocationId { get; set; }

    /// <summary>Allocated slot ID when allocated</summary>
    public string? SlotId { get; set; }

    /// <summary>Allocation source: draw, sameDay, reallocation, manualCorrection</summary>
    public string? AllocationSource { get; set; }

    /// <summary>Draw attempt ID when allocated via Draw</summary>
    public string? DrawAttemptId { get; set; }

    /// <summary>When request was submitted</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>When final decision was made</summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>Last updated timestamp for projection freshness</summary>
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
