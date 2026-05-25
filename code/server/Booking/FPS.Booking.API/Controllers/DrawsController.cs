using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.SharedKernel.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [Authorize(Roles = "admin")]
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
            Steps: result.Steps.Select(s => new DrawLifecycleStepResponse(s.Name, s.Status, s.Summary, s.OccurredAt)).ToList(),
            Decisions: result.Decisions.Select(d => new DrawLifecycleDecisionResponse(d.BookingReference, d.Outcome, d.SlotReference, d.Reason)).ToList(),
            Tier2CandidateSequence: result.Tier2CandidateSequence));
    }

    [HttpGet("{date}/status")]
    [ProducesResponseType(typeof(DrawStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        if (result is null) return NotFound();

        return Ok(new DrawStatusResponse(
            result.DrawKey,
            result.Status,
            result.RequestCount,
            result.AllocatedCount,
            result.RejectedCount,
            result.WaitlistedCount,
            result.CompanyCarOverflowCount,
            result.SummaryRejectionReasons,
            result.AlgorithmVersion,
            result.Seed,
            result.AuditReference,
            result.StartedAt,
            result.CompletedAt));
    }
}
