using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.SharedKernel.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace FPS.Booking.API.Controllers;

[ApiController]
[Route("draws")]
[Authorize]
public sealed class DrawsController : ControllerBase
{
    private readonly IMediator mediator;
    private readonly ICurrentUser currentUser;

    public DrawsController(IMediator mediator, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(currentUser);
        this.mediator = mediator;
        this.currentUser = currentUser;
    }

    [HttpPost("trigger")]
    [Authorize(Roles = "admin,hr_manager")]
    [ProducesResponseType(typeof(TriggerDrawResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(TriggerDrawResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerDraw(
        [FromBody] TriggerDrawRequest body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await mediator.Send(new TriggerDrawCommand(
            TenantId: currentUser.TenantId,
            LocationId: body.LocationId,
            Date: body.Date,
            TimeSlotStart: body.TimeSlotStart,
            TimeSlotEnd: body.TimeSlotEnd,
            Reason: body.Reason),
            cancellationToken);

        var response = new TriggerDrawResponse(
            result.DrawAttemptId,
            result.Status,
            result.AllocatedCount,
            result.RejectedCount,
            result.WaitlistedCount);

        return result.WasAlreadyCompleted ? Ok(response) : Accepted(response);
    }

    [HttpGet("{date}/lifecycle")]
    [Authorize(Roles = "auditor,admin")]
    [ProducesResponseType(typeof(DrawLifecycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDrawLifecycle(
        DateOnly date,
        [FromQuery] string locationId,
        [FromQuery] DateTime timeSlotStart,
        [FromQuery] DateTime timeSlotEnd,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await mediator.Send(
            new GetDrawLifecycleQuery(currentUser.TenantId, locationId, date, timeSlotStart, timeSlotEnd),
            cancellationToken);

        if (result is null) return NotFound();

        return Ok(new DrawLifecycleResponse(
            DrawKey: result.DrawKey,
            LocationId: result.LocationId,
            Date: result.Date.ToString("yyyy-MM-dd"),
            Status: result.Status,
            AlgorithmVersion: result.AlgorithmVersion,
            Seed: result.Seed,
            AuditReference: result.AuditReference,
            RequestCount: result.RequestCount,
            AllocatedCount: result.AllocatedCount,
            RejectedCount: result.RejectedCount,
            WaitlistedCount: result.WaitlistedCount,
            StartedAt: result.StartedAt,
            CompletedAt: result.CompletedAt,
            Steps: result.Steps.Select(s => new DrawLifecycleStepResponse(s.Name, s.Status, s.Summary, s.OccurredAt, s.ErrorMessage)).ToList(),
            Decisions: result.Decisions.Select(d => new DrawLifecycleDecisionResponse(d.BookingReference, d.Outcome, d.SlotReference, d.Reason)).ToList(),
            Tier2CandidateSequence: result.Tier2CandidateSequence));
    }

    [HttpGet("{date}/status")]
    [ProducesResponseType(typeof(DrawStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDrawStatus(
        DateOnly date,
        [FromQuery] string locationId,
        [FromQuery] DateTime timeSlotStart,
        [FromQuery] DateTime timeSlotEnd,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await mediator.Send(
            new GetDrawStatusQuery(currentUser.TenantId, locationId, date, timeSlotStart, timeSlotEnd),
            cancellationToken);

        return Ok(new DrawStatusResponse(
            Status: result.Status,
            StartedAt: result.StartedAt,
            CompletedAt: result.CompletedAt,
            DemandLevel: result.DemandLevel,
            CutOffAt: result.CutOffAt,
            NextDrawAt: result.NextDrawAt,
            TimeZone: result.TimeZone,
            RequestWindowStatus: result.RequestWindowStatus,
            ScheduleStatus: result.ScheduleStatus,
            ScheduleSource: result.ScheduleSource,
            LastCalculatedAt: result.LastCalculatedAt,
            SafeMessage: result.SafeMessage,
            RequestCount: result.RequestCount,
            AvailableSpotCount: result.AvailableSpotCount,
            CanRequest: result.CanRequest,
            CannotRequestReason: result.CannotRequestReason));
    }

    [HttpGet("my-outcomes")]
    [ProducesResponseType(typeof(MyDrawOutcomesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyDrawOutcomes(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var effectiveFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-180));
        var effectiveTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var summaries = await mediator.Send(
            new GetMyDrawOutcomesQuery(currentUser.TenantId, currentUser.UserId, effectiveFrom, effectiveTo),
            cancellationToken);

        var draws = summaries.Select(s => new MyDrawOutcomeSummaryResponse(
            s.Date, s.TimeSlot, s.LocationId, s.DrawStatus,
            s.AllocatedCount, s.TotalRequests, s.CompletedAt,
            s.MyOutcome, s.MyReason, s.MyAllocatedSlotId)).ToList();

        return Ok(new MyDrawOutcomesResponse(draws));
    }

    [HttpGet("outcomes")]
    [Authorize(Roles = "admin,hr_manager")]
    [ProducesResponseType(typeof(HrDrawOutcomesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHrDrawOutcomes(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? locationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var effectiveFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-90));
        var effectiveTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var summaries = await mediator.Send(
            new GetHrDrawOutcomesQuery(currentUser.TenantId, locationId, effectiveFrom, effectiveTo),
            cancellationToken);

        var draws = summaries.Select(s => new HrDrawOutcomeSummaryResponse(
            Date: s.Date,
            TimeSlot: s.TimeSlot,
            LocationId: s.LocationId,
            DrawStatus: s.DrawStatus,
            AllocatedCount: s.AllocatedCount,
            RejectedCount: s.RejectedCount,
            WaitlistedCount: s.WaitlistedCount,
            TotalRequests: s.TotalRequests,
            CompletedAt: s.CompletedAt,
            Outcomes: s.Outcomes.Select(o => new HrDrawOutcomeItemResponse(
                o.RequestId, o.RequestorRef, o.Outcome, o.ReasonCode, o.Reason, o.AllocatedSlotId
            )).ToList())).ToList();

        return Ok(new HrDrawOutcomesResponse(draws));
    }
}
