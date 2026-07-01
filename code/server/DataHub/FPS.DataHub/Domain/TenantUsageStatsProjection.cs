namespace FPS.DataHub.Domain;

/// <summary>
/// PLAT005A — per-tenant monthly usage-statistics ledger row. Platform-readable attribution of
/// platform usage by tenant, with no billing/pricing semantics and no PII. One row per
/// (TenantId, PeriodMonth). Recomputed deterministically from the booking-outcome and
/// draw-history projections, so duplicate event delivery cannot double-count.
/// </summary>
public sealed class TenantUsageStatsProjection
{
    /// <summary>Primary key.</summary>
    public long Id { get; set; }

    /// <summary>Tenant the statistics are attributed to.</summary>
    public string TenantId { get; set; } = "";

    /// <summary>First day of the calendar month this row aggregates (period key).</summary>
    public DateOnly PeriodMonth { get; set; }

    /// <summary>Distinct requestors seen in booking outcomes for the month (a count, never the ids).</summary>
    public int ActiveRequestorCount { get; set; }

    /// <summary>Booking requests recorded for the month.</summary>
    public int BookingRequestCount { get; set; }

    /// <summary>Draw runs recorded for the month.</summary>
    public int DrawRunCount { get; set; }

    /// <summary>Requests whose latest outcome is Allocated.</summary>
    public int AllocatedCount { get; set; }

    /// <summary>Requests whose latest outcome is Rejected.</summary>
    public int RejectedCount { get; set; }

    /// <summary>Requests whose latest outcome is Cancelled.</summary>
    public int CancelledCount { get; set; }

    /// <summary>Requests whose latest outcome is Expired.</summary>
    public int ExpiredCount { get; set; }

    /// <summary>Requests whose latest outcome is NoShow.</summary>
    public int NoShowCount { get; set; }

    /// <summary>Requests whose latest outcome is Used.</summary>
    public int UsedCount { get; set; }

    /// <summary>Last recompute timestamp for projection freshness.</summary>
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
