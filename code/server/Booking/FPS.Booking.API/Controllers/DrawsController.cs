using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.API.Controllers;

[ApiController]
[Route("draws")]
public sealed class DrawsController : ControllerBase
{
    private readonly IMediator mediator;

    public DrawsController(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        this.mediator = mediator;
    }

    [HttpPost("trigger")]
    [ProducesResponseType(typeof(TriggerDrawResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(TriggerDrawResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerDraw(
        [FromBody] TriggerDrawRequest body,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new TriggerDrawCommand(
            TenantId: tenantId,
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
        [FromHeader(Name = "X-Tenant-Id")] string tenantId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetDrawStatusQuery(tenantId, locationId, date, timeSlotStart, timeSlotEnd),
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
