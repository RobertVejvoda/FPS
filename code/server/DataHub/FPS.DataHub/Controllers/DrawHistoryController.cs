using FPS.DataHub.Application;
using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Controllers;

[ApiController]
[Authorize]
public sealed class DrawHistoryController(
    DataHubDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Get HR Draw History for the authenticated tenant.
    /// Returns completed Draws with allocation/rejection/waitlist counts.
    /// Requires HR or admin role.
    /// </summary>
    [HttpGet("/datahub/draw-history")]
    [Authorize(Policy = "RequireHrOrAdmin")]
    public async Task<IActionResult> GetDrawHistory(
        [FromQuery] string? locationId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized();

        pageSize = Math.Min(Math.Max(pageSize, 1), 100);
        var skip = (page - 1) * pageSize;

        var query = db.DrawHistory.Where(d => d.TenantId == tenantId);

        if (!string.IsNullOrEmpty(locationId))
            query = query.Where(d => d.LocationId == locationId);

        if (fromDate.HasValue)
            query = query.Where(d => d.Date >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(d => d.Date <= toDate.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.StartedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(d => new DrawHistoryDto
            {
                DrawAttemptId = d.DrawAttemptId,
                LocationId = d.LocationId,
                Date = d.Date,
                TimeSlot = d.TimeSlot,
                Status = d.Status,
                TriggerSource = d.TriggerSource,
                StartedAt = d.StartedAt,
                CompletedAt = d.CompletedAt,
                AllocatedCount = d.AllocatedCount,
                RejectedCount = d.RejectedCount,
                WaitlistedCount = d.WaitlistedCount,
                SafeFailureReason = d.SafeFailureReason
            })
            .ToListAsync(ct);

        return Ok(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }

    /// <summary>
    /// Get projection freshness information.
    /// Shows last processed event timestamp and staleness indicators.
    /// </summary>
    [HttpGet("/datahub/projection-health")]
    [Authorize(Policy = "RequireHrOrAdmin")]
    public async Task<IActionResult> GetProjectionHealth(CancellationToken ct)
    {
        var tenantId = currentUser.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized();

        var lastDrawUpdate = await db.DrawHistory
            .Where(d => d.TenantId == tenantId)
            .MaxAsync(d => (DateTimeOffset?)d.LastUpdatedAt, ct);

        var lastOutcomeUpdate = await db.BookingOutcomes
            .Where(b => b.TenantId == tenantId)
            .MaxAsync(b => (DateTimeOffset?)b.LastUpdatedAt, ct);

        var lastProcessedEvent = await db.EventInbox
            .Where(e => e.TenantId == tenantId && e.ProcessedAt != null)
            .OrderByDescending(e => e.ProcessedAt)
            .Select(e => new { e.ProcessedAt, e.OccurredAt })
            .FirstOrDefaultAsync(ct);

        var pendingCount = await db.EventInbox
            .Where(e => e.TenantId == tenantId && e.ProcessingStatus == EventProcessingStatus.Pending)
            .CountAsync(ct);

        var failedCount = await db.EventInbox
            .Where(e => e.TenantId == tenantId && e.ProcessingStatus == EventProcessingStatus.Failed)
            .CountAsync(ct);

        var poisonedCount = await db.EventInbox
            .Where(e => e.TenantId == tenantId && e.ProcessingStatus == EventProcessingStatus.Poisoned)
            .CountAsync(ct);

        var lagSeconds = lastProcessedEvent is not null
            ? (lastProcessedEvent.ProcessedAt - lastProcessedEvent.OccurredAt)?.TotalSeconds
            : null;

        return Ok(new
        {
            LastDrawUpdate = lastDrawUpdate,
            LastOutcomeUpdate = lastOutcomeUpdate,
            LastProcessedEventAt = lastProcessedEvent?.ProcessedAt,
            LastEventOccurredAt = lastProcessedEvent?.OccurredAt,
            ProcessingLagSeconds = lagSeconds,
            PendingEvents = pendingCount,
            FailedEvents = failedCount,
            PoisonedEvents = poisonedCount,
            Status = poisonedCount > 0 ? "degraded" :
                     failedCount > 10 ? "degraded" :
                     pendingCount > 100 ? "lagging" :
                     lagSeconds > 60 ? "lagging" :
                     "healthy"
        });
    }
}

public sealed class DrawHistoryDto
{
    public string DrawAttemptId { get; set; } = "";
    public string LocationId { get; set; } = "";
    public DateOnly Date { get; set; }
    public string TimeSlot { get; set; } = "";
    public string Status { get; set; } = "";
    public string? TriggerSource { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AllocatedCount { get; set; }
    public int RejectedCount { get; set; }
    public int WaitlistedCount { get; set; }
    public string? SafeFailureReason { get; set; }
}
