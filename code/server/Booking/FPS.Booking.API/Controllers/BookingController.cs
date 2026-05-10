using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.API.Controllers;

[ApiController]
[Route("bookings")]
public class BookingController : ControllerBase
{
    private readonly IMediator _mediator;

    public BookingController(IMediator mediator) => _mediator = mediator;

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

        var result = await _mediator.Send(command, cancellationToken);

        var response = new SubmitBookingResponse(
            result.RequestId,
            result.Status,
            result.RejectionCode,
            result.Reason);

        return result.Status == "Pending"
            ? Accepted(response)
            : UnprocessableEntity(response);
    }

    [HttpGet("{requestId:guid}/status")]
    public IActionResult GetBookingStatus(Guid requestId) => StatusCode(501, "Not implemented");

    [HttpPost("{requestId:guid}/arrival")]
    public IActionResult ConfirmArrival(Guid requestId) => StatusCode(501, "Not implemented");

    [HttpDelete("{requestId:guid}")]
    public IActionResult CancelBooking(Guid requestId) => StatusCode(501, "Not implemented");
}
