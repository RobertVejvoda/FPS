using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Application;

/// <summary>
/// PLAT005A — maintains the per-tenant monthly usage-statistics ledger
/// (<see cref="TenantUsageStatsProjection"/>).
///
/// Design: rather than incrementing counters per event (which is not idempotent under duplicate
/// Dapr delivery or handler retry), each relevant event triggers a deterministic <b>recompute</b>
/// of the affected (tenant, month) bucket from the already-idempotent booking-outcome and
/// draw-history projections. Those projections are keyed by business id (BookingRequestId /
/// DrawAttemptId), so counting them is stable: reprocessing the same event — or rebuilding from
/// scratch — yields the same row and never double-counts.
///
/// Registered after <see cref="BookingProjectionHandler"/> (dispatch is sequential in registration
/// order), so the upstream outcome/draw row for the current event is already written when we
/// recompute. The ledger holds counts only — no requestor ids or PII.
/// </summary>
public sealed class UsageStatsProjectionHandler(DataHubDbContext db) : IProjectionHandler
{
    public bool CanHandle(string eventType) => eventType switch
    {
        "booking.drawStarted" => true,
        "booking.drawCompleted" => true,
        "booking.drawFailed" => true,
        "booking.requestSubmitted" => true,
        "booking.requestAllocated" or "booking.slotAllocated" => true,
        "booking.requestRejected" => true,
        "booking.requestCancelled" => true,
        "booking.usageConfirmed" => true,
        "booking.noShowRecorded" => true,
        "booking.requestExpired" => true,
        _ => false
    };

    public async Task HandleAsync(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var month = await ResolvePeriodMonthAsync(envelope, ct);
        await RecomputeAsync(envelope.TenantId, month, ct);
    }

    // The month bucket is the booking/draw date's month. Prefer the date carried in the payload
    // (the slot/draw target date, matching the projection's Date column); fall back to the stored
    // projection row, then the event time.
    private async Task<DateOnly> ResolvePeriodMonthAsync(BookingEventEnvelope e, CancellationToken ct)
    {
        var p = e.Payload;
        if (DateOnly.TryParse(p.Date, out var fromPayload))
            return FirstOfMonth(fromPayload);

        if (!string.IsNullOrEmpty(p.BookingRequestId))
        {
            var outcome = await db.BookingOutcomes.AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookingRequestId == p.BookingRequestId, ct);
            if (outcome is not null) return FirstOfMonth(outcome.Date);
        }

        if (!string.IsNullOrEmpty(p.DrawAttemptId))
        {
            var draw = await db.DrawHistory.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DrawAttemptId == p.DrawAttemptId, ct);
            if (draw is not null) return FirstOfMonth(draw.Date);
        }

        return FirstOfMonth(DateOnly.FromDateTime(e.OccurredAt));
    }

    private async Task RecomputeAsync(string tenantId, DateOnly month, CancellationToken ct)
    {
        var monthEnd = month.AddMonths(1);
        var outcomes = db.BookingOutcomes.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.Date >= month && b.Date < monthEnd);

        var row = await db.TenantUsageStats
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.PeriodMonth == month, ct);
        if (row is null)
        {
            row = new TenantUsageStatsProjection { TenantId = tenantId, PeriodMonth = month };
            db.TenantUsageStats.Add(row);
        }

        row.BookingRequestCount = await outcomes.CountAsync(ct);
        row.ActiveRequestorCount = await outcomes.Select(b => b.RequestorId).Distinct().CountAsync(ct);
        row.AllocatedCount = await outcomes.CountAsync(b => b.FinalStatus == "Allocated", ct);
        row.RejectedCount = await outcomes.CountAsync(b => b.FinalStatus == "Rejected", ct);
        row.CancelledCount = await outcomes.CountAsync(b => b.FinalStatus == "Cancelled", ct);
        row.ExpiredCount = await outcomes.CountAsync(b => b.FinalStatus == "Expired", ct);
        row.NoShowCount = await outcomes.CountAsync(b => b.FinalStatus == "NoShow", ct);
        row.UsedCount = await outcomes.CountAsync(b => b.FinalStatus == "Used", ct);
        row.DrawRunCount = await db.DrawHistory.AsNoTracking()
            .CountAsync(d => d.TenantId == tenantId && d.Date >= month && d.Date < monthEnd, ct);
        row.LastUpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    private static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);
}
