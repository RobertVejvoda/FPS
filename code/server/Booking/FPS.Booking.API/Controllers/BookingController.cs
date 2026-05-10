using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.Booking.Application.Repositories;
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

    [HttpGet]
    [ProducesResponseType(typeof(GetMyBookingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyBookings(
        [FromHeader(Name = "X-Tenant-Id")] string tenantId,
        [FromHeader(Name = "X-Requestor-Id")] string requestorId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? status,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new GetMyBookingsQuery(tenantId, requestorId, from, to, status, pageSize, cursor),
            cancellationToken);

        return Ok(new GetMyBookingsResponse(result.Items, result.NextCursor));
    }

    [HttpPost("{requestId:guid}/confirm-usage")]
    [ProducesResponseType(typeof(ConfirmUsageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmUsage(
        Guid requestId,
        [FromBody] ConfirmUsageRequest body,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId,
        [FromHeader(Name = "X-Requestor-Id")] string requestorId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new ConfirmSlotUsageCommand(
                RequestId: requestId,
                TenantId: tenantId,
                RequestorId: requestorId,
                ConfirmationSource: body.ConfirmationSource,
                ConfirmedAt: body.ConfirmedAt,
                SourceEventId: body.SourceEventId),
                cancellationToken);

            return Ok(new ConfirmUsageResponse(result.RequestId, result.Status, result.ConfirmedAt, result.WasAlreadyConfirmed));
        }
        catch (FPS.Booking.Application.Exceptions.BookingNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (FPS.Booking.Domain.Exceptions.BookingException ex)
        {
            return UnprocessableEntity(new { ex.Message });
        }
    }

    [HttpPost("{requestId:guid}/manual-corrections")]
    [ProducesResponseType(typeof(ManualCorrectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ApplyManualCorrection(
        Guid requestId,
        [FromBody] ManualCorrectionRequest body,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId,
        [FromHeader(Name = "X-Actor-Id")] string actor,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new ApplyManualCorrectionCommand(
                RequestId: requestId,
                TenantId: tenantId,
                Actor: actor,
                CorrectionType: body.CorrectionType,
                OldValue: body.OldValue,
                NewValue: body.NewValue,
                Reason: body.Reason,
                EffectiveAt: body.EffectiveAt),
                cancellationToken);

            return Ok(result);
        }
        catch (BookingNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (CorrectionConflictException ex)
        {
            return Conflict(new { ex.Message });
        }
        catch (FPS.Booking.Domain.Exceptions.BookingException ex)
        {
            return UnprocessableEntity(new { ex.Message });
        }
    }

    [HttpGet("{requestId:guid}/status")]
    public IActionResult GetBookingStatus(Guid requestId) => StatusCode(501, "Not implemented");

    [HttpPost("{requestId:guid}/arrival")]
    public IActionResult ConfirmArrival(Guid requestId) => StatusCode(501, "Not implemented");
}
