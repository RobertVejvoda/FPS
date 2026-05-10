using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.API.Controllers;

[ApiController]
[Route("bookings")]
public sealed class EvaluationsController : ControllerBase
{
    private readonly IMediator mediator;

    public EvaluationsController(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        this.mediator = mediator;
    }

    [HttpPost("no-show-evaluation")]
    [ProducesResponseType(typeof(EvaluateNoShowResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> EvaluateNoShow(
        [FromBody] NoShowEvaluationRequest body,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new EvaluateNoShowCommand(
            TenantId: tenantId,
            LocationId: body.LocationId,
            Date: body.Date,
            TimeSlotStart: body.TimeSlotStart,
            TimeSlotEnd: body.TimeSlotEnd,
            Reason: body.Reason),
            cancellationToken);

        return Ok(result);
    }
}
