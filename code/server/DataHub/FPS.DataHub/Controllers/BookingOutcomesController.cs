using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Controllers;

[ApiController]
[Authorize]
public sealed class BookingOutcomesController(
    DataHubDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Get Past Draw Outcomes for the authenticated employee.
    /// Returns only the authenticated employee's own outcomes.
    /// Employee privacy enforced: cannot see other employees' outcomes.
    /// </summary>
    [HttpGet("/datahub/my-outcomes")]
    public async Task<IActionResult> GetMyOutcomes(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId;
        var userId = currentUser.UserId;

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId))
            return Unauthorized();

        pageSize = Math.Min(Math.Max(pageSize, 1), 100);
        var skip = (page - 1) * pageSize;

        var query = db.BookingOutcomes
            .Where(b => b.TenantId == tenantId && b.RequestorId == userId);

        if (fromDate.HasValue)
            query = query.Where(b => b.Date >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(b => b.Date <= toDate.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(b => b.Date)
            .ThenByDescending(b => b.DecidedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(b => new BookingOutcomeDto
            {
                BookingRequestId = b.BookingRequestId,
                LocationId = b.LocationId,
                Date = b.Date,
                TimeSlot = b.TimeSlot,
                FinalStatus = b.FinalStatus,
                ReasonCode = b.ReasonCode,
                SafeReasonText = b.SafeReasonText,
                AllocationSource = b.AllocationSource,
                SubmittedAt = b.SubmittedAt,
                DecidedAt = b.DecidedAt
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
    /// Get booking outcomes for a specific Draw (HR/admin only).
    /// Shows safe per-request outcomes without exposing lottery internals.
    /// </summary>
    [HttpGet("/datahub/draw-outcomes/{drawAttemptId}")]
    [Authorize(Roles = "hr_manager,admin")]
    public async Task<IActionResult> GetDrawOutcomes(
        string drawAttemptId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized();

        pageSize = Math.Min(Math.Max(pageSize, 1), 100);
        var skip = (page - 1) * pageSize;

        // Verify the Draw belongs to this tenant
        var draw = await db.DrawHistory.FirstOrDefaultAsync(
            d => d.DrawAttemptId == drawAttemptId && d.TenantId == tenantId, ct);

        if (draw is null)
            return NotFound(new { Message = "Draw not found or not accessible" });

        var query = db.BookingOutcomes
            .Where(b => b.TenantId == tenantId && b.DrawAttemptId == drawAttemptId);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(b => b.FinalStatus)
            .ThenBy(b => b.RequestorId)
            .Skip(skip)
            .Take(pageSize)
            .Select(b => new BookingOutcomeWithRequestorDto
            {
                BookingRequestId = b.BookingRequestId,
                RequestorId = b.RequestorId,
                LocationId = b.LocationId,
                Date = b.Date,
                TimeSlot = b.TimeSlot,
                FinalStatus = b.FinalStatus,
                ReasonCode = b.ReasonCode,
                SafeReasonText = b.SafeReasonText,
                AllocationSource = b.AllocationSource,
                SlotId = b.SlotId,
                DecidedAt = b.DecidedAt
            })
            .ToListAsync(ct);

        return Ok(new
        {
            Draw = new
            {
                draw.DrawAttemptId,
                draw.LocationId,
                draw.Date,
                draw.TimeSlot,
                draw.Status,
                draw.AllocatedCount,
                draw.RejectedCount,
                draw.WaitlistedCount,
                draw.CompletedAt
            },
            Outcomes = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }
}

public class BookingOutcomeDto
{
    public string BookingRequestId { get; set; } = "";
    public string LocationId { get; set; } = "";
    public DateOnly Date { get; set; }
    public string TimeSlot { get; set; } = "";
    public string FinalStatus { get; set; } = "";
    public string? ReasonCode { get; set; }
    public string? SafeReasonText { get; set; }
    public string? AllocationSource { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public sealed class BookingOutcomeWithRequestorDto : BookingOutcomeDto
{
    public string RequestorId { get; set; } = "";
    public string? SlotId { get; set; }
}
