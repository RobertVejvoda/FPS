using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.Booking.Domain.Exceptions;
using FPS.SharedKernel.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.API.Controllers;

[ApiController]
[Route("bookings")]
[Authorize]
public sealed class BookingController : ControllerBase
{
    private readonly IMediator mediator;
    private readonly ICurrentUser currentUser;

    public BookingController(IMediator mediator, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(currentUser);
        this.mediator = mediator;
        this.currentUser = currentUser;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SubmitBookingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(SubmitBookingResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitBookingRequest(
        [FromBody] SubmitBookingRequest body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var command = new SubmitBookingRequestCommand(
            TenantId: currentUser.TenantId,
            RequestorId: currentUser.UserId,
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
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        try
        {
            var result = await mediator.Send(
                new CancelBookingCommand(requestId, currentUser.TenantId, currentUser.UserId, reason ?? "Cancelled by requestor"),
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
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? status,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var result = await mediator.Send(
            new GetMyBookingsQuery(currentUser.TenantId, currentUser.UserId, from, to, status, pageSize, cursor),
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
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        try
        {
            var result = await mediator.Send(new ConfirmSlotUsageCommand(
                RequestId: requestId,
                TenantId: currentUser.TenantId,
                RequestorId: currentUser.UserId,
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
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        try
        {
            var result = await mediator.Send(new ApplyManualCorrectionCommand(
                RequestId: requestId,
                TenantId: currentUser.TenantId,
                Actor: currentUser.UserId,
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
