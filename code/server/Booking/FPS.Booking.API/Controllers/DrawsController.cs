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

    /// <summary>
    /// Get the full Draw lifecycle with step-level tracking for auditors and authorized administrators.
    /// Includes per-booking decisions, deterministic evidence, and correlation metadata.
    /// </summary>
    [HttpGet("{date}/lifecycle")]
    [Authorize(Roles = "admin,auditor")]
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
            result.DrawKey,
            result.TenantId,
            result.LocationId,
            result.Date,
            result.Status,
            result.Seed,
            result.AlgorithmVersion,
            result.StartedAt,
            result.CompletedAt,
            result.Steps.Select(s => new DrawLifecycleStepResponse(
                s.StepName,
                s.Status,
                s.StartedAt,
                s.CompletedAt,
                s.ReasonCode,
                s.Summary
            )).ToList(),
            result.AllocatedCount,
            result.RejectedCount,
            result.WaitlistedCount,
            result.Decisions.Select(d => new DrawDecisionResponse(
                d.RequestId,
                d.RequestorId,
                d.Outcome,
                d.SlotId,
                d.Reason
            )).ToList(),
            result.Tier2CandidateSequence,
            result.CorrelationId,
            result.TraceId
        ));
    }
}
