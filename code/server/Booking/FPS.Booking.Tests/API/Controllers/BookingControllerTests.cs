using FPS.Booking.Controllers;
using FPS.Booking.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.Booking.Domain.Exceptions;
using FPS.SharedKernel.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Booking.Tests.Controllers;

public sealed class BookingControllerTests
{
    private readonly Mock<IMediator> mediator = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly BookingController controller;

    public BookingControllerTests()
    {
        currentUser.Setup(u => u.TenantId).Returns("tenant-1");
        currentUser.Setup(u => u.UserId).Returns("user-1");
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);

        controller = new BookingController(mediator.Object, currentUser.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    // ── POST /bookings ────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitBookingRequest_ValidRequest_Returns202Accepted()
    {
        mediator
            .Setup(m => m.Send(It.IsAny<SubmitBookingRequestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitBookingRequestResult(Guid.NewGuid(), "Pending", null, null));

        var result = await controller.SubmitBookingRequest(ValidSubmitBody(), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var body = Assert.IsType<SubmitBookingResponse>(accepted.Value);
        Assert.Equal("Pending", body.Status);
    }

    [Fact]
    public async Task SubmitBookingRequest_DuplicateRequest_Returns422()
    {
        mediator
            .Setup(m => m.Send(It.IsAny<SubmitBookingRequestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitBookingRequestResult(
                Guid.NewGuid(), "Rejected", "DuplicateRequest",
                "You already have a request for an overlapping time slot."));

        var result = await controller.SubmitBookingRequest(ValidSubmitBody(), CancellationToken.None);

        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var body = Assert.IsType<SubmitBookingResponse>(unprocessable.Value);
        Assert.Equal("DuplicateRequest", body.RejectionCode);
    }

    [Fact]
    public async Task SubmitBookingRequest_MapsCommandFieldsFromCurrentUser()
    {
        currentUser.Setup(u => u.TenantId).Returns("tenant-42");
        currentUser.Setup(u => u.UserId).Returns("user-99");

        SubmitBookingRequestCommand? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<SubmitBookingRequestCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<SubmitBookingRequestResult>, CancellationToken>(
                (cmd, _) => captured = (SubmitBookingRequestCommand)cmd)
            .ReturnsAsync(new SubmitBookingRequestResult(Guid.NewGuid(), "Pending", null, null));

        var body = ValidSubmitBody();
        await controller.SubmitBookingRequest(body, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("tenant-42", captured.TenantId);
        Assert.Equal("user-99", captured.RequestorId);
        Assert.Equal(body.FacilityId, captured.FacilityId);
    }

    // ── DELETE /bookings/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task CancelBooking_PendingRequest_Returns200()
    {
        var requestId = Guid.NewGuid();
        mediator
            .Setup(m => m.Send(It.IsAny<CancelBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancelBookingResult(requestId, "Cancelled"));

        var result = await controller.CancelBooking(requestId, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CancelBooking_NotFound_Returns404()
    {
        mediator
            .Setup(m => m.Send(It.IsAny<CancelBookingCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingNotFoundException(Guid.NewGuid()));

        var result = await controller.CancelBooking(Guid.NewGuid(), null, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CancelBooking_AlreadyCancelled_Returns422()
    {
        mediator
            .Setup(m => m.Send(It.IsAny<CancelBookingCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingException("Only pending or allocated requests can be cancelled"));

        var result = await controller.CancelBooking(Guid.NewGuid(), null, CancellationToken.None);

        Assert.IsType<UnprocessableEntityObjectResult>(result);
    }

    // ── GET /bookings ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyBookings_ReturnsOkWithItems()
    {
        var items = new List<BookingListItem>
        {
            new(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                new TimeOnly(9, 0), new TimeOnly(17, 0), null,
                "Pending", null, null, null, "cancel", DateTime.UtcNow, DateTime.UtcNow)
        };
        mediator
            .Setup(m => m.Send(It.IsAny<GetMyBookingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult(items, null));

        var result = await controller.GetMyBookings(null, null, null, 50, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<GetMyBookingsResponse>(ok.Value);
        Assert.Single(body.Items);
        Assert.Null(body.NextCursor);
    }

    [Fact]
    public async Task GetMyBookings_EmptyResult_ReturnsOkWithEmptyList()
    {
        mediator
            .Setup(m => m.Send(It.IsAny<GetMyBookingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([], null));

        var result = await controller.GetMyBookings(null, null, null, 50, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<GetMyBookingsResponse>(ok.Value);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task GetMyBookings_PassesTenantAndUserFromCurrentUser()
    {
        currentUser.Setup(u => u.TenantId).Returns("t-99");
        currentUser.Setup(u => u.UserId).Returns("u-42");

        GetMyBookingsQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetMyBookingsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<BookingListResult>, CancellationToken>(
                (q, _) => captured = (GetMyBookingsQuery)q)
            .ReturnsAsync(new BookingListResult([], null));

        await controller.GetMyBookings(null, null, null, 50, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("t-99", captured.TenantId);
        Assert.Equal("u-42", captured.RequestorId);
    }

    [Fact]
    public async Task GetMyBookings_WithCursor_PassesCursorToQuery()
    {
        GetMyBookingsQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetMyBookingsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<BookingListResult>, CancellationToken>(
                (q, _) => captured = (GetMyBookingsQuery)q)
            .ReturnsAsync(new BookingListResult([], "next-page"));

        await controller.GetMyBookings(null, null, null, 10, "some-cursor", CancellationToken.None);

        Assert.Equal("some-cursor", captured?.Cursor);
    }

    // ── POST /bookings/{id}/confirm-usage ─────────────────────────────────────

    [Fact]
    public async Task ConfirmUsage_AllocatedRequest_Returns200()
    {
        var requestId = Guid.NewGuid();
        var confirmedAt = DateTime.UtcNow;
        mediator.Setup(m => m.Send(It.IsAny<ConfirmSlotUsageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmSlotUsageResult(requestId, "Used", confirmedAt, false));

        var result = await controller.ConfirmUsage(requestId, new ConfirmUsageRequest("EmployeeSelf"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ConfirmUsageResponse>(ok.Value);
        Assert.Equal("Used", body.Status);
        Assert.False(body.WasAlreadyConfirmed);
    }

    [Fact]
    public async Task ConfirmUsage_AlreadyConfirmed_Returns200WithFlag()
    {
        mediator.Setup(m => m.Send(It.IsAny<ConfirmSlotUsageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmSlotUsageResult(Guid.NewGuid(), "Used", DateTime.UtcNow, true));

        var result = await controller.ConfirmUsage(Guid.NewGuid(), new ConfirmUsageRequest("EmployeeSelf"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(((ConfirmUsageResponse)ok.Value!).WasAlreadyConfirmed);
    }

    [Fact]
    public async Task ConfirmUsage_NotFound_Returns404()
    {
        mediator.Setup(m => m.Send(It.IsAny<ConfirmSlotUsageCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingNotFoundException(Guid.NewGuid()));

        var result = await controller.ConfirmUsage(Guid.NewGuid(), new ConfirmUsageRequest("EmployeeSelf"), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ConfirmUsage_NotAllocated_Returns422()
    {
        mediator.Setup(m => m.Send(It.IsAny<ConfirmSlotUsageCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FPS.Booking.Domain.Exceptions.BookingException("Only allocated requests can be confirmed as used"));

        var result = await controller.ConfirmUsage(Guid.NewGuid(), new ConfirmUsageRequest("EmployeeSelf"), CancellationToken.None);

        Assert.IsType<UnprocessableEntityObjectResult>(result);
    }

    // ── POST /bookings/{id}/manual-corrections ────────────────────────────────

    [Fact]
    public async Task ApplyManualCorrection_ValidRequest_Returns200()
    {
        var requestId = Guid.NewGuid();
        mediator.Setup(m => m.Send(It.IsAny<ApplyManualCorrectionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManualCorrectionResult(requestId, "status", "Allocated", DateTime.UtcNow));

        var result = await controller.ApplyManualCorrection(
            requestId, new ManualCorrectionRequest("status", "Pending", "Allocated", "HR override"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ApplyManualCorrection_ActorComesFromCurrentUser()
    {
        currentUser.Setup(u => u.UserId).Returns("hr-user-from-token");

        ApplyManualCorrectionCommand? captured = null;
        mediator.Setup(m => m.Send(It.IsAny<ApplyManualCorrectionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ManualCorrectionResult>, CancellationToken>(
                (cmd, _) => captured = (ApplyManualCorrectionCommand)cmd)
            .ReturnsAsync(new ManualCorrectionResult(Guid.NewGuid(), "status", "Allocated", DateTime.UtcNow));

        await controller.ApplyManualCorrection(
            Guid.NewGuid(), new ManualCorrectionRequest("status", "Pending", "Allocated", "HR override"),
            CancellationToken.None);

        Assert.Equal("hr-user-from-token", captured?.Actor);
    }

    [Fact]
    public async Task ApplyManualCorrection_OldValueMismatch_Returns409()
    {
        mediator.Setup(m => m.Send(It.IsAny<ApplyManualCorrectionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CorrectionConflictException(Guid.NewGuid(), "status", "Allocated", "Pending"));

        var result = await controller.ApplyManualCorrection(
            Guid.NewGuid(), new ManualCorrectionRequest("status", "Allocated", "Pending", "Fix"),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task ApplyManualCorrection_MissingReason_Returns422()
    {
        mediator.Setup(m => m.Send(It.IsAny<ApplyManualCorrectionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FPS.Booking.Domain.Exceptions.BookingException("A reason is required for manual corrections."));

        var result = await controller.ApplyManualCorrection(
            Guid.NewGuid(), new ManualCorrectionRequest("status", "Pending", "Allocated", ""),
            CancellationToken.None);

        Assert.IsType<UnprocessableEntityObjectResult>(result);
    }

    // ── GET /bookings/hr/employees/{userId}/history ───────────────────────────

    [Fact]
    public async Task GetHrEmployeeHistory_ValidRequest_Returns200WithResult()
    {
        var summary = new HrEmployeeHistorySummary(Total: 3, Allocated: 2, Rejected: 1, Cancelled: 0, Pending: 0);
        var expected = new HrEmployeeHistoryResult("employee-1", summary, [], null, 3);
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrEmployeeHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await controller.GetHrEmployeeHistory(
            "employee-1", null, null, null, 50, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<HrEmployeeHistoryResult>(ok.Value);
        Assert.Equal(2, body.Summary.Allocated);
        Assert.Equal(3, body.TotalCount);
    }

    [Fact]
    public async Task GetHrEmployeeHistory_MissingTenant_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await controller.GetHrEmployeeHistory(
            "employee-1", null, null, null, 50, null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetHrEmployeeHistory_BlankUserId_Returns400()
    {
        var result = await controller.GetHrEmployeeHistory(
            "   ", null, null, null, 50, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetHrEmployeeHistory_PassesAuthenticatedTenantToQuery()
    {
        currentUser.Setup(u => u.TenantId).Returns("tenant-isolated");
        GetHrEmployeeHistoryQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrEmployeeHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<HrEmployeeHistoryResult>, CancellationToken>(
                (q, _) => captured = (GetHrEmployeeHistoryQuery)q)
            .ReturnsAsync(new HrEmployeeHistoryResult("e-1", new HrEmployeeHistorySummary(0, 0, 0, 0, 0), [], null));

        await controller.GetHrEmployeeHistory("e-1", null, null, null, 50, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("tenant-isolated", captured.TenantId);
        Assert.Equal("e-1", captured.RequestorId);
    }

    [Fact]
    public async Task GetHrEmployeeHistory_NoDateRangeProvided_AppliesDefaultLast30Days()
    {
        GetHrEmployeeHistoryQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrEmployeeHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<HrEmployeeHistoryResult>, CancellationToken>(
                (q, _) => captured = (GetHrEmployeeHistoryQuery)q)
            .ReturnsAsync(new HrEmployeeHistoryResult("e-1", new HrEmployeeHistorySummary(0, 0, 0, 0, 0), [], null));

        await controller.GetHrEmployeeHistory("e-1", null, null, null, 50, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.NotNull(captured.From);
        Assert.NotNull(captured.To);
        var span = captured.To!.Value.DayNumber - captured.From!.Value.DayNumber;
        Assert.Equal(30, span);
    }

    [Fact]
    public async Task GetHrEmployeeHistory_ExplicitDateRange_NotOverriddenWithDefault()
    {
        GetHrEmployeeHistoryQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrEmployeeHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<HrEmployeeHistoryResult>, CancellationToken>(
                (q, _) => captured = (GetHrEmployeeHistoryQuery)q)
            .ReturnsAsync(new HrEmployeeHistoryResult("e-1", new HrEmployeeHistorySummary(0, 0, 0, 0, 0), [], null));

        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        await controller.GetHrEmployeeHistory("e-1", from, to, null, 50, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(from, captured.From);
        Assert.Equal(to, captured.To);
    }

    [Fact]
    public async Task GetHrEmployeeHistory_OnlyFromProvided_NoDefaultApplied()
    {
        GetHrEmployeeHistoryQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrEmployeeHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<HrEmployeeHistoryResult>, CancellationToken>(
                (q, _) => captured = (GetHrEmployeeHistoryQuery)q)
            .ReturnsAsync(new HrEmployeeHistoryResult("e-1", new HrEmployeeHistorySummary(0, 0, 0, 0, 0), [], null));

        var from = new DateOnly(2026, 1, 1);
        await controller.GetHrEmployeeHistory("e-1", from, null, null, 50, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(from, captured.From);
        Assert.Null(captured.To);
    }

    [Fact]
    public async Task GetHrEmployeeHistory_PassesStatusFilterToQuery()
    {
        GetHrEmployeeHistoryQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrEmployeeHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<HrEmployeeHistoryResult>, CancellationToken>(
                (q, _) => captured = (GetHrEmployeeHistoryQuery)q)
            .ReturnsAsync(new HrEmployeeHistoryResult("e-1", new HrEmployeeHistorySummary(0, 0, 0, 0, 0), [], null));

        await controller.GetHrEmployeeHistory(
            "e-1",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
            "Rejected", 25, "cursor", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Rejected", captured.StatusFilter);
        Assert.Equal(25, captured.PageSize);
        Assert.Equal("cursor", captured.Cursor);
    }

    // ── GET /bookings/operations/slots/{slotId}/history (issue #471) ─────────

    [Fact]
    public async Task GetHrSlotHistory_ValidRequest_Returns200WithResult()
    {
        var expected = new HrSlotHistoryResult("M1-1", [], null, 0);
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrSlotHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await controller.GetHrSlotHistory(
            "M1-1", null, null, null, 50, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<HrSlotHistoryResult>(ok.Value);
        Assert.Equal("M1-1", body.SlotId);
    }

    [Fact]
    public async Task GetHrSlotHistory_MissingTenant_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await controller.GetHrSlotHistory(
            "M1-1", null, null, null, 50, null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetHrSlotHistory_BlankSlotId_Returns400()
    {
        var result = await controller.GetHrSlotHistory(
            "   ", null, null, null, 50, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetHrSlotHistory_PassesAuthenticatedTenantAndLocationToQuery()
    {
        currentUser.Setup(u => u.TenantId).Returns("tenant-isolated");
        GetHrSlotHistoryQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrSlotHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<HrSlotHistoryResult>, CancellationToken>(
                (q, _) => captured = (GetHrSlotHistoryQuery)q)
            .ReturnsAsync(new HrSlotHistoryResult("M1-1", [], null));

        await controller.GetHrSlotHistory("M1-1", "Prague", null, null, 50, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("tenant-isolated", captured.TenantId);
        Assert.Equal("Prague", captured.LocationId);
        Assert.Equal("M1-1", captured.SlotId);
    }

    [Fact]
    public async Task GetHrSlotHistory_NoDateRangeProvided_AppliesDefaultLast30Days()
    {
        GetHrSlotHistoryQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrSlotHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<HrSlotHistoryResult>, CancellationToken>(
                (q, _) => captured = (GetHrSlotHistoryQuery)q)
            .ReturnsAsync(new HrSlotHistoryResult("M1-1", [], null));

        await controller.GetHrSlotHistory("M1-1", null, null, null, 50, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.NotNull(captured.From);
        Assert.NotNull(captured.To);
        var span = captured.To!.Value.DayNumber - captured.From!.Value.DayNumber;
        Assert.Equal(30, span);
    }

    [Fact]
    public async Task GetHrSlotHistory_ExplicitDateRange_NotOverridden()
    {
        GetHrSlotHistoryQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrSlotHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<HrSlotHistoryResult>, CancellationToken>(
                (q, _) => captured = (GetHrSlotHistoryQuery)q)
            .ReturnsAsync(new HrSlotHistoryResult("M1-1", [], null));

        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        await controller.GetHrSlotHistory("M1-1", null, from, to, 50, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(from, captured.From);
        Assert.Equal(to, captured.To);
    }

    [Fact]
    public async Task GetHrSlotHistory_PassesPagingToQuery()
    {
        GetHrSlotHistoryQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetHrSlotHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<HrSlotHistoryResult>, CancellationToken>(
                (q, _) => captured = (GetHrSlotHistoryQuery)q)
            .ReturnsAsync(new HrSlotHistoryResult("M1-1", [], null));

        await controller.GetHrSlotHistory(
            "M1-1", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 25, "cursor", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(25, captured.PageSize);
        Assert.Equal("cursor", captured.Cursor);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SubmitBookingRequest ValidSubmitBody() => new(
        FacilityId: Guid.NewGuid().ToString(),
        LocationId: null,
        LicensePlate: "XYZ-999",
        VehicleType: "Sedan",
        IsElectric: false,
        RequiresAccessibleSpot: false,
        IsCompanyCar: false,
        PlannedArrivalTime: DateTime.UtcNow.AddDays(1).Date.AddHours(9),
        PlannedDepartureTime: DateTime.UtcNow.AddDays(1).Date.AddHours(17));
}
