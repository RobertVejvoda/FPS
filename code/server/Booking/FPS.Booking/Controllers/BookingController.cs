using FPS.Booking.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.Booking.Domain.Exceptions;
using FPS.SharedKernel.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.Controllers;

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

        if (!Guid.TryParse(body.FacilityId, out _))
            return BadRequest(new { error = "FacilityId must be a valid UUID." });

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

        return Ok(new GetMyBookingsResponse(result.Items, result.NextCursor, result.TotalCount));
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

    [HttpGet("operations")]
    [Authorize(Roles = "hr_manager,admin")]
    [ProducesResponseType(typeof(GetHrBookingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHrBookings(
        [FromQuery] string? locationId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? status,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await mediator.Send(
            new GetHrBookingListQuery(currentUser.TenantId, locationId, from, to, status, pageSize, cursor),
            cancellationToken);

        return Ok(new GetHrBookingsResponse(result.Items, result.NextCursor, result.TotalCount));
    }

    // Default window for HR employee parking history. Issue #464 calls for
    // "default recent period, e.g. last 30/45 days" — 30 days matches the
    // smallest practical window and stays cheap to project later from DataHub.
    private const int DefaultHistoryWindowDays = 30;

    [HttpGet("hr/employees/{userId}/history")]
    [Authorize(Roles = "hr_manager,admin")]
    [ProducesResponseType(typeof(HrEmployeeHistoryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHrEmployeeHistory(
        string userId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? status,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { Message = "userId is required." });

        // Default window: last 30 days through today when caller omits both.
        if (from is null && to is null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            from = today.AddDays(-DefaultHistoryWindowDays);
            to = today;
        }

        var result = await mediator.Send(
            new GetHrEmployeeHistoryQuery(currentUser.TenantId, userId, from, to, status, pageSize, cursor),
            cancellationToken);

        return Ok(result);
    }

    // Slot-history page size is clamped at the controller boundary so a
    // caller can't pass a negative size (Take throws and the endpoint 500s)
    // or a huge value (leaks the full tenant ops history in one response).
    // 100 matches the existing ParkingSlotController.GetSlotHistory cap.
    private const int MaxSlotHistoryPageSize = 100;

    [HttpGet("operations/slots/{slotId}/history")]
    [Authorize(Roles = "hr_manager,admin")]
    [ProducesResponseType(typeof(HrSlotHistoryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHrSlotHistory(
        string slotId,
        [FromQuery] string? locationId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(slotId))
            return BadRequest(new { Message = "slotId is required." });

        // Default window: last 30 days through today when caller omits both.
        // Matches the employee-history default and keeps the drawer fast on
        // tenants with long-running ops indices.
        if (from is null && to is null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            from = today.AddDays(-DefaultHistoryWindowDays);
            to = today;
        }

        // Clamp before the query so the repository's Take(pageSize) never
        // sees a negative or wildly large value (Codex review on #473).
        var clampedPageSize = Math.Clamp(pageSize, 1, MaxSlotHistoryPageSize);

        var result = await mediator.Send(
            new GetHrSlotHistoryQuery(currentUser.TenantId, locationId, slotId, from, to, clampedPageSize, cursor),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{requestId:guid}/hr-cancel")]
    [Authorize(Roles = "hr_manager,admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> HrCancelBooking(
        Guid requestId,
        [FromBody] HrCancelRequest body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Reason))
            return BadRequest(new { Message = "Reason is required for HR cancellation." });

        try
        {
            var result = await mediator.Send(
                new CancelBookingCommand(requestId, currentUser.TenantId, currentUser.UserId, body.Reason, ActorType: "hr_manager"),
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
}
