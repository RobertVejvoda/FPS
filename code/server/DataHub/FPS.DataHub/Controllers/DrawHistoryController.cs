using FPS.DataHub.Application;
using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FPS.DataHub.Controllers;

[ApiController]
[Authorize]
public sealed class DrawHistoryController(
    DataHubDbContext db,
    ICurrentUser currentUser,
    ILogger<DrawHistoryController> logger) : ControllerBase
{
    // Safe status value constants used for in-progress detection.
    private const string StatusRunning = "Running";
    private const string StatusInProgress = "InProgress";
    /// <summary>
    /// Get HR Draw History for the authenticated tenant.
    /// Returns completed Draws with allocation/rejection/waitlist counts.
    /// Requires HR or admin role.
    /// </summary>
    [HttpGet("/datahub/draw-history")]
    [Authorize(Roles = "hr_manager,admin")]
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
                RunReason = d.RunReason,
                TriggeredBy = d.TriggeredBy,
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
    /// Get Draw workflow progress for a specific Draw attempt.
    /// Returns the Draw summary and ordered lifecycle steps projected from booking events.
    /// Lifecycle steps are only available for completed or failed Draws; in-progress Draws
    /// show current status only and note that projection lag may apply.
    /// Requires HR, auditor, or admin role.
    /// </summary>
    [HttpGet("/datahub/draw-history/{drawAttemptId}/progress")]
    [Authorize(Roles = "hr_manager,admin,auditor")]
    public async Task<IActionResult> GetDrawProgress(
        string drawAttemptId,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized();

        var draw = await db.DrawHistory.FirstOrDefaultAsync(
            d => d.TenantId == tenantId && d.DrawAttemptId == drawAttemptId, ct);

        if (draw is null)
            return NotFound(new { Message = "Draw attempt not found or does not belong to this tenant." });

        List<DrawProgressStepDto>? steps = null;
        string? stepsNote = null;

        if (draw.LifecycleStepsJson is not null)
        {
            try
            {
                var projections = JsonSerializer.Deserialize<List<DrawProgressStepProjection>>(
                    draw.LifecycleStepsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                steps = projections?
                    .Select(s => new DrawProgressStepDto
                    {
                        StepName = s.StepName,
                        Status = s.Status,
                        Summary = s.Summary,
                        OccurredAt = s.OccurredAt
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to deserialise LifecycleStepsJson for draw {DrawAttemptId}", draw.DrawAttemptId);
                stepsNote = "Lifecycle steps could not be deserialised; projection may be stale.";
            }
        }
        else if (draw.Status == StatusRunning || draw.Status == StatusInProgress)
        {
            stepsNote = "Draw is in progress. Lifecycle steps are projected after the Draw completes.";
        }
        else
        {
            stepsNote = "Lifecycle steps not yet projected. The Draw may have completed before DRAW009 was deployed.";
        }

        return Ok(new DrawProgressResponse
        {
            DrawAttemptId = draw.DrawAttemptId,
            LocationId = draw.LocationId,
            Date = draw.Date,
            TimeSlot = draw.TimeSlot,
            Status = draw.Status,
            TriggerSource = draw.TriggerSource,
            RunReason = draw.RunReason,
            TriggeredBy = draw.TriggeredBy,
            StartedAt = draw.StartedAt,
            CompletedAt = draw.CompletedAt,
            AllocatedCount = draw.AllocatedCount,
            RejectedCount = draw.RejectedCount,
            WaitlistedCount = draw.WaitlistedCount,
            SafeFailureReason = draw.SafeFailureReason,
            LastProjectedAt = draw.LastUpdatedAt,
            Steps = steps,
            StepsNote = stepsNote
        });
    }

    /// <summary>
    /// Get projection freshness information.
    /// Shows last processed event timestamp and staleness indicators.
    /// </summary>
    [HttpGet("/datahub/projection-health")]
    [Authorize(Roles = "hr_manager,admin")]
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
    // HR-supplied reason for manual / recovery runs (issue #472).
    public string? RunReason { get; set; }
    // Operator-safe identifier of the actor that triggered the run.
    public string? TriggeredBy { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AllocatedCount { get; set; }
    public int RejectedCount { get; set; }
    public int WaitlistedCount { get; set; }
    public string? SafeFailureReason { get; set; }
}

/// <summary>
/// DRAW009: Safe Draw workflow progress read model for HR and auditor views.
/// Contains the Draw summary plus ordered lifecycle steps (when available).
/// </summary>
public sealed class DrawProgressResponse
{
    public string DrawAttemptId { get; set; } = "";
    public string LocationId { get; set; } = "";
    public DateOnly Date { get; set; }
    public string TimeSlot { get; set; } = "";
    public string Status { get; set; } = "";
    public string? TriggerSource { get; set; }
    public string? RunReason { get; set; }
    public string? TriggeredBy { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AllocatedCount { get; set; }
    public int RejectedCount { get; set; }
    public int WaitlistedCount { get; set; }
    public string? SafeFailureReason { get; set; }
    /// <summary>When DataHub last updated this projection row.</summary>
    public DateTimeOffset LastProjectedAt { get; set; }
    /// <summary>
    /// Ordered lifecycle steps. Null when the Draw is still in progress or lifecycle
    /// steps were not projected (pre-DRAW009 draws). Check StepsNote for context.
    /// </summary>
    public List<DrawProgressStepDto>? Steps { get; set; }
    /// <summary>
    /// Explains why Steps may be null or incomplete. Null when steps are available.
    /// </summary>
    public string? StepsNote { get; set; }
}

/// <summary>Safe Draw workflow lifecycle step returned in the progress read model.</summary>
public sealed class DrawProgressStepDto
{
    public string StepName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Summary { get; set; }
    public DateTime? OccurredAt { get; set; }
}
