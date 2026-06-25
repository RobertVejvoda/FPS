using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Controllers;

[ApiController]
[Authorize(Roles = "hr_manager,admin,report_viewer")]
public sealed class OperationalMetricsController(
    DataHubDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    private const int DefaultLookbackDays = 30;

    // ── GET /datahub/metrics/dashboard ──────────────────────────────────────────
    // Tenant-scoped aggregate totals. Use as the landing summary for demo/report views.
    [HttpGet("/datahub/metrics/dashboard")]
    public async Task<IActionResult> Dashboard(
        [FromQuery] string? locationId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var tenantId = currentUser.TenantId;
        var (from, to) = ResolveDateRange(fromDate, toDate);

        var outcomesQuery = db.BookingOutcomes
            .Where(b => b.TenantId == tenantId && b.Date >= from && b.Date <= to);
        if (!string.IsNullOrEmpty(locationId))
            outcomesQuery = outcomesQuery.Where(b => b.LocationId == locationId);

        var outcomes = await outcomesQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total      = g.Count(),
                Allocated  = g.Count(b => b.FinalStatus == "Allocated" || b.FinalStatus == "Used" || b.FinalStatus == "NoShow"),
                Rejected   = g.Count(b => b.FinalStatus == "Rejected"),
                Cancelled  = g.Count(b => b.FinalStatus == "Cancelled"),
                NoShow     = g.Count(b => b.FinalStatus == "NoShow"),
                Used       = g.Count(b => b.FinalStatus == "Used"),
                Waitlisted = g.Count(b => b.FinalStatus == "Waitlisted"),
                Expired    = g.Count(b => b.FinalStatus == "Expired"),
                Submitted  = g.Count(b => b.FinalStatus == "Submitted"),
            })
            .FirstOrDefaultAsync(ct);

        var drawsQuery = db.DrawHistory
            .Where(d => d.TenantId == tenantId && d.Date >= from && d.Date <= to);
        if (!string.IsNullOrEmpty(locationId))
            drawsQuery = drawsQuery.Where(d => d.LocationId == locationId);

        var draws = await drawsQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total  = g.Count(),
                Failed = g.Count(d => d.Status == "Failed"),
            })
            .FirstOrDefaultAsync(ct);

        var freshness = await db.BookingOutcomes
            .Where(b => b.TenantId == tenantId)
            .MaxAsync(b => (DateTimeOffset?)b.LastUpdatedAt, ct);

        int demand    = outcomes?.Total     ?? 0;
        int allocated = outcomes?.Allocated ?? 0;

        return Ok(new DashboardResponse(
            Period:               new DateRangeDto(from, to),
            LocationId:           locationId,
            Demand:               demand,
            Allocated:            allocated,
            Rejected:             outcomes?.Rejected   ?? 0,
            Cancelled:            outcomes?.Cancelled  ?? 0,
            NoShow:               outcomes?.NoShow     ?? 0,
            Used:                 outcomes?.Used       ?? 0,
            Waitlisted:           outcomes?.Waitlisted ?? 0,
            Expired:              outcomes?.Expired    ?? 0,
            Submitted:            outcomes?.Submitted  ?? 0,
            AllocationRate:       demand == 0 ? 0 : Math.Round(allocated * 100.0 / demand, 1),
            TotalDraws:           draws?.Total  ?? 0,
            FailedDraws:          draws?.Failed ?? 0,
            ProjectionFreshnessAt: freshness));
    }

    // ── GET /datahub/metrics/daily ───────────────────────────────────────────────
    // Per-day/location/timeslot summary rows. Paged for date-range reports.
    [HttpGet("/datahub/metrics/daily")]
    public async Task<IActionResult> Daily(
        [FromQuery] string? locationId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

        var tenantId = currentUser.TenantId;
        var (from, to) = ResolveDateRange(fromDate, toDate);

        var query = db.BookingOutcomes
            .Where(b => b.TenantId == tenantId && b.Date >= from && b.Date <= to);
        if (!string.IsNullOrEmpty(locationId))
            query = query.Where(b => b.LocationId == locationId);

        var grouped = query.GroupBy(b => new { b.Date, b.LocationId, b.TimeSlot });
        var total   = await grouped.CountAsync(ct);

        var rows = await grouped
            .OrderByDescending(g => g.Key.Date)
            .ThenBy(g => g.Key.LocationId)
            .ThenBy(g => g.Key.TimeSlot)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new
            {
                g.Key.Date,
                g.Key.LocationId,
                g.Key.TimeSlot,
                Demand     = g.Count(),
                Allocated  = g.Count(b => b.FinalStatus == "Allocated" || b.FinalStatus == "Used" || b.FinalStatus == "NoShow"),
                Rejected   = g.Count(b => b.FinalStatus == "Rejected"),
                Cancelled  = g.Count(b => b.FinalStatus == "Cancelled"),
                NoShow     = g.Count(b => b.FinalStatus == "NoShow"),
                Waitlisted = g.Count(b => b.FinalStatus == "Waitlisted"),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new DailySummaryRow(
            Date:           r.Date,
            LocationId:     r.LocationId,
            TimeSlot:       r.TimeSlot,
            Demand:         r.Demand,
            Allocated:      r.Allocated,
            Rejected:       r.Rejected,
            Cancelled:      r.Cancelled,
            NoShow:         r.NoShow,
            Waitlisted:     r.Waitlisted,
            AllocationRate: r.Demand == 0 ? 0 : Math.Round(r.Allocated * 100.0 / r.Demand, 1)));

        return Ok(new { Items = items, Page = page, PageSize = pageSize, Total = total });
    }

    // ── GET /datahub/metrics/utilization ────────────────────────────────────────
    // Per-location demand and allocation summary.
    [HttpGet("/datahub/metrics/utilization")]
    public async Task<IActionResult> Utilization(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var tenantId = currentUser.TenantId;
        var (from, to) = ResolveDateRange(fromDate, toDate);

        var rows = await db.BookingOutcomes
            .Where(b => b.TenantId == tenantId && b.Date >= from && b.Date <= to)
            .GroupBy(b => b.LocationId)
            .Select(g => new
            {
                LocationId       = g.Key,
                Demand           = g.Count(),
                Allocated        = g.Count(b => b.FinalStatus == "Allocated" || b.FinalStatus == "Used" || b.FinalStatus == "NoShow"),
                Rejected         = g.Count(b => b.FinalStatus == "Rejected"),
                Cancelled        = g.Count(b => b.FinalStatus == "Cancelled"),
                UniqueRequestors = g.Select(b => b.RequestorId).Distinct().Count(),
            })
            .OrderBy(r => r.LocationId)
            .ToListAsync(ct);

        var items = rows.Select(r => new UtilizationRow(
            LocationId:       r.LocationId,
            Demand:           r.Demand,
            Allocated:        r.Allocated,
            AllocationRate:   r.Demand == 0 ? 0 : Math.Round(r.Allocated * 100.0 / r.Demand, 1),
            Rejected:         r.Rejected,
            Cancelled:        r.Cancelled,
            UniqueRequestors: r.UniqueRequestors));

        return Ok(new { Period = new DateRangeDto(from, to), Items = items });
    }

    // ── GET /datahub/metrics/reason-codes ───────────────────────────────────────
    // Rejection, cancellation, and no-show reason-code frequency.
    [HttpGet("/datahub/metrics/reason-codes")]
    public async Task<IActionResult> ReasonCodes(
        [FromQuery] string? locationId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var tenantId = currentUser.TenantId;
        var (from, to) = ResolveDateRange(fromDate, toDate);

        var query = db.BookingOutcomes
            .Where(b => b.TenantId == tenantId && b.Date >= from && b.Date <= to
                     && (b.FinalStatus == "Rejected" || b.FinalStatus == "Cancelled" || b.FinalStatus == "NoShow")
                     && b.ReasonCode != null);
        if (!string.IsNullOrEmpty(locationId))
            query = query.Where(b => b.LocationId == locationId);

        var rows = await query
            .GroupBy(b => new { b.FinalStatus, b.ReasonCode })
            .Select(g => new { g.Key.FinalStatus, ReasonCode = g.Key.ReasonCode!, Count = g.Count() })
            .ToListAsync(ct);

        return Ok(new ReasonCodeResponse(
            Period:        new DateRangeDto(from, to),
            LocationId:    locationId,
            Rejections:    rows.Where(r => r.FinalStatus == "Rejected")
                               .Select(r => new ReasonCodeCount(r.ReasonCode, r.Count))
                               .OrderByDescending(r => r.Count).ToList(),
            Cancellations: rows.Where(r => r.FinalStatus == "Cancelled")
                               .Select(r => new ReasonCodeCount(r.ReasonCode, r.Count))
                               .OrderByDescending(r => r.Count).ToList(),
            NoShows:       rows.Where(r => r.FinalStatus == "NoShow")
                               .Select(r => new ReasonCodeCount(r.ReasonCode, r.Count))
                               .OrderByDescending(r => r.Count).ToList()));
    }

    // ── GET /datahub/metrics/employee-impact ────────────────────────────────────
    // Per-employee allocation fairness summary. Matches existing /reports/parking/employee-impact
    // role contract (hr_manager, admin, report_viewer) — all three may access this endpoint.
    [HttpGet("/datahub/metrics/employee-impact")]
    public async Task<IActionResult> EmployeeImpact(
        [FromQuery] string? locationId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

        var tenantId = currentUser.TenantId;
        var (from, to) = ResolveDateRange(fromDate, toDate);

        var query = db.BookingOutcomes
            .Where(b => b.TenantId == tenantId && b.Date >= from && b.Date <= to);
        if (!string.IsNullOrEmpty(locationId))
            query = query.Where(b => b.LocationId == locationId);

        var grouped = query.GroupBy(b => b.RequestorId);
        var total   = await grouped.CountAsync(ct);

        var rows = await grouped
            .Select(g => new
            {
                RequestorId = g.Key,
                Demand      = g.Count(),
                Allocated   = g.Count(b => b.FinalStatus == "Allocated" || b.FinalStatus == "Used" || b.FinalStatus == "NoShow"),
                Rejected    = g.Count(b => b.FinalStatus == "Rejected"),
                Cancelled   = g.Count(b => b.FinalStatus == "Cancelled"),
                NoShow      = g.Count(b => b.FinalStatus == "NoShow"),
            })
            .OrderByDescending(r => r.Demand)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(r => new EmployeeImpactRow(
            RequestorId:    r.RequestorId,
            Demand:         r.Demand,
            Allocated:      r.Allocated,
            AllocationRate: r.Demand == 0 ? 0 : Math.Round(r.Allocated * 100.0 / r.Demand, 1),
            Rejected:       r.Rejected,
            Cancelled:      r.Cancelled,
            NoShow:         r.NoShow));

        return Ok(new { Period = new DateRangeDto(from, to), Items = items, Page = page, PageSize = pageSize, Total = total });
    }

    // ── GET /datahub/metrics/operational-exceptions ─────────────────────────────
    // Failed draws, draws that completed with zero allocations, and projection lag.
    // Matches existing /reports/parking/operational-exceptions role contract.
    [HttpGet("/datahub/metrics/operational-exceptions")]
    public async Task<IActionResult> OperationalExceptions(
        [FromQuery] string? locationId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var tenantId = currentUser.TenantId;
        var (from, to) = ResolveDateRange(fromDate, toDate);

        var drawQuery = db.DrawHistory
            .Where(d => d.TenantId == tenantId && d.Date >= from && d.Date <= to);
        if (!string.IsNullOrEmpty(locationId))
            drawQuery = drawQuery.Where(d => d.LocationId == locationId);

        var failedDraws = await drawQuery
            .Where(d => d.Status == "Failed")
            .OrderByDescending(d => d.LastUpdatedAt)
            .Select(d => new FailedDrawDto(
                d.DrawAttemptId, d.LocationId, d.Date, d.TimeSlot,
                d.SafeFailureReason, d.LastUpdatedAt))
            .ToListAsync(ct);

        var zeroAllocationDraws = await drawQuery
            .Where(d => d.Status == "Completed" && d.AllocatedCount == 0
                     && (d.RejectedCount > 0 || d.WaitlistedCount > 0))
            .OrderByDescending(d => d.Date)
            .Select(d => new ZeroAllocationDrawDto(
                d.DrawAttemptId, d.LocationId, d.Date, d.TimeSlot,
                d.RejectedCount, d.WaitlistedCount, d.LastUpdatedAt))
            .ToListAsync(ct);

        var lastProcessed = await db.EventInbox
            .Where(e => e.TenantId == tenantId && e.ProcessedAt != null)
            .MaxAsync(e => (DateTimeOffset?)e.ProcessedAt, ct);

        var lastOccurred = await db.EventInbox
            .Where(e => e.TenantId == tenantId && e.ProcessedAt != null)
            .MaxAsync(e => (DateTimeOffset?)e.OccurredAt, ct);

        double? lagSeconds = (lastProcessed.HasValue && lastOccurred.HasValue)
            ? (lastProcessed.Value - lastOccurred.Value).TotalSeconds
            : null;

        return Ok(new OperationalExceptionsResponse(
            Period:              new DateRangeDto(from, to),
            LocationId:          locationId,
            FailedDraws:         failedDraws,
            ZeroAllocationDraws: zeroAllocationDraws,
            ProjectionLagSeconds: lagSeconds,
            LastProjectedAt:     lastProcessed));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static (DateOnly from, DateOnly to) ResolveDateRange(DateOnly? fromDate, DateOnly? toDate)
    {
        var to   = toDate   ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = fromDate ?? to.AddDays(-DefaultLookbackDays);
        return (from, to);
    }
}

// ── Response records ─────────────────────────────────────────────────────────

public sealed record DateRangeDto(DateOnly From, DateOnly To);

public sealed record DashboardResponse(
    DateRangeDto Period,
    string? LocationId,
    int Demand,
    int Allocated,
    int Rejected,
    int Cancelled,
    int NoShow,
    int Used,
    int Waitlisted,
    int Expired,
    int Submitted,
    double AllocationRate,
    int TotalDraws,
    int FailedDraws,
    DateTimeOffset? ProjectionFreshnessAt);

public sealed record DailySummaryRow(
    DateOnly Date,
    string LocationId,
    string TimeSlot,
    int Demand,
    int Allocated,
    int Rejected,
    int Cancelled,
    int NoShow,
    int Waitlisted,
    double AllocationRate);

public sealed record UtilizationRow(
    string LocationId,
    int Demand,
    int Allocated,
    double AllocationRate,
    int Rejected,
    int Cancelled,
    int UniqueRequestors);

public sealed record ReasonCodeCount(string ReasonCode, int Count);

public sealed record ReasonCodeResponse(
    DateRangeDto Period,
    string? LocationId,
    IReadOnlyList<ReasonCodeCount> Rejections,
    IReadOnlyList<ReasonCodeCount> Cancellations,
    IReadOnlyList<ReasonCodeCount> NoShows);

public sealed record EmployeeImpactRow(
    string RequestorId,
    int Demand,
    int Allocated,
    double AllocationRate,
    int Rejected,
    int Cancelled,
    int NoShow);

public sealed record FailedDrawDto(
    string DrawAttemptId,
    string LocationId,
    DateOnly Date,
    string TimeSlot,
    string? SafeFailureReason,
    DateTimeOffset LastUpdatedAt);

public sealed record ZeroAllocationDrawDto(
    string DrawAttemptId,
    string LocationId,
    DateOnly Date,
    string TimeSlot,
    int RejectedCount,
    int WaitlistedCount,
    DateTimeOffset LastUpdatedAt);

public sealed record OperationalExceptionsResponse(
    DateRangeDto Period,
    string? LocationId,
    IReadOnlyList<FailedDrawDto> FailedDraws,
    IReadOnlyList<ZeroAllocationDrawDto> ZeroAllocationDraws,
    double? ProjectionLagSeconds,
    DateTimeOffset? LastProjectedAt);
