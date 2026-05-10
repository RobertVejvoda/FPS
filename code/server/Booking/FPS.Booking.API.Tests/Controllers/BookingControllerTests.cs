using FPS.Booking.API.Controllers;
using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.Booking.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Booking.API.Tests.Controllers;

public sealed class BookingControllerTests
{
    private readonly Mock<IMediator> mediator = new();
    private readonly BookingController controller;

    public BookingControllerTests()
    {
        controller = new BookingController(mediator.Object);
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

        var result = await controller.SubmitBookingRequest(
            ValidSubmitBody(), "tenant-1", Guid.NewGuid().ToString(), CancellationToken.None);

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

        var result = await controller.SubmitBookingRequest(
            ValidSubmitBody(), "tenant-1", Guid.NewGuid().ToString(), CancellationToken.None);

        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var body = Assert.IsType<SubmitBookingResponse>(unprocessable.Value);
        Assert.Equal("DuplicateRequest", body.RejectionCode);
    }

    [Fact]
    public async Task SubmitBookingRequest_MapsCommandFieldsCorrectly()
    {
        SubmitBookingRequestCommand? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<SubmitBookingRequestCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<SubmitBookingRequestResult>, CancellationToken>(
                (cmd, _) => captured = (SubmitBookingRequestCommand)cmd)
            .ReturnsAsync(new SubmitBookingRequestResult(Guid.NewGuid(), "Pending", null, null));

        var body = ValidSubmitBody();
        await controller.SubmitBookingRequest(body, "tenant-42", "user-99", CancellationToken.None);

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

        var result = await controller.CancelBooking(
            requestId, "tenant-1", Guid.NewGuid().ToString(), null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CancelBooking_NotFound_Returns404()
    {
        mediator
            .Setup(m => m.Send(It.IsAny<CancelBookingCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingNotFoundException(Guid.NewGuid()));

        var result = await controller.CancelBooking(
            Guid.NewGuid(), "tenant-1", Guid.NewGuid().ToString(), null, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CancelBooking_AlreadyCancelled_Returns422()
    {
        mediator
            .Setup(m => m.Send(It.IsAny<CancelBookingCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingException("Only pending or allocated requests can be cancelled"));

        var result = await controller.CancelBooking(
            Guid.NewGuid(), "tenant-1", Guid.NewGuid().ToString(), null, CancellationToken.None);

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
                "Pending", null, null, "cancel", DateTime.UtcNow, DateTime.UtcNow)
        };
        mediator
            .Setup(m => m.Send(It.IsAny<GetMyBookingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult(items, null));

        var result = await controller.GetMyBookings(
            "tenant-1", "user-1", null, null, null, 50, null, CancellationToken.None);

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

        var result = await controller.GetMyBookings(
            "tenant-1", "user-1", null, null, null, 50, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<GetMyBookingsResponse>(ok.Value);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task GetMyBookings_PassesTenantAndUserToQuery()
    {
        GetMyBookingsQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetMyBookingsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<BookingListResult>, CancellationToken>(
                (q, _) => captured = (GetMyBookingsQuery)q)
            .ReturnsAsync(new BookingListResult([], null));

        await controller.GetMyBookings("t-99", "u-42", null, null, null, 50, null, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("t-99", captured!.TenantId);
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

        await controller.GetMyBookings("t-1", "u-1", null, null, null, 10, "some-cursor", CancellationToken.None);

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

        var result = await controller.ConfirmUsage(requestId, new ConfirmUsageRequest("EmployeeSelf"),
            "tenant-1", Guid.NewGuid().ToString(), CancellationToken.None);

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

        var result = await controller.ConfirmUsage(Guid.NewGuid(), new ConfirmUsageRequest("EmployeeSelf"),
            "tenant-1", Guid.NewGuid().ToString(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ConfirmUsageResponse>(ok.Value);
        Assert.True(body.WasAlreadyConfirmed);
    }

    [Fact]
    public async Task ConfirmUsage_NotFound_Returns404()
    {
        mediator.Setup(m => m.Send(It.IsAny<ConfirmSlotUsageCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingNotFoundException(Guid.NewGuid()));

        var result = await controller.ConfirmUsage(Guid.NewGuid(), new ConfirmUsageRequest("EmployeeSelf"),
            "tenant-1", Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ConfirmUsage_NotAllocated_Returns422()
    {
        mediator.Setup(m => m.Send(It.IsAny<ConfirmSlotUsageCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FPS.Booking.Domain.Exceptions.BookingException("Only allocated requests can be confirmed as used"));

        var result = await controller.ConfirmUsage(Guid.NewGuid(), new ConfirmUsageRequest("EmployeeSelf"),
            "tenant-1", Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.IsType<UnprocessableEntityObjectResult>(result);
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
