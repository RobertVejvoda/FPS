using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Exceptions;
using FPS.Booking.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.API.Controllers;

[ApiController]
[Route("bookings")]
public sealed class BookingController : ControllerBase
{
    private readonly IMediator mediator;

    public BookingController(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        this.mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SubmitBookingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(SubmitBookingResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitBookingRequest(
        [FromBody] SubmitBookingRequest body,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId,
        [FromHeader(Name = "X-Requestor-Id")] string requestorId,
        CancellationToken cancellationToken)
    {
        var command = new SubmitBookingRequestCommand(
            TenantId: tenantId,
            RequestorId: requestorId,
            FacilityId: body.FacilityId,
            LocationId: body.LocationId,
            LicensePlate: body.LicensePlate,
            VehicleType: body.VehicleType,
            IsElectric: body.IsElectric,
            RequiresAccessibleSpot: body.RequiresAccessibleSpot,
            IsCompanyCar: body.IsCompanyCar,
            PlannedArrivalTime: body.PlannedArrivalTime,
            PlannedDepartureTime: body.PlannedDepartureTime);

        var result = await mediator.Send(command, cancellationToken);

        var response = new SubmitBookingResponse(
            result.RequestId, result.Status, result.RejectionCode, result.Reason);

        return result.Status == "Pending"
            ? Accepted(response)
            : UnprocessableEntity(response);
    }

    [HttpDelete("{requestId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelBooking(
        Guid requestId,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId,
        [FromHeader(Name = "X-Requestor-Id")] string requestorId,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new CancelBookingCommand(requestId, tenantId, requestorId, reason ?? "Cancelled by requestor"),
                cancellationToken);

            return Ok(new { result.RequestId, result.Status });
        }
        catch (BookingNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (BookingException ex)
        {
            return UnprocessableEntity(new { ex.Message });
        }
    }

    [HttpGet("{requestId:guid}/status")]
    public IActionResult GetBookingStatus(Guid requestId) => StatusCode(501, "Not implemented");

    [HttpPost("{requestId:guid}/arrival")]
    public IActionResult ConfirmArrival(Guid requestId) => StatusCode(501, "Not implemented");
}
