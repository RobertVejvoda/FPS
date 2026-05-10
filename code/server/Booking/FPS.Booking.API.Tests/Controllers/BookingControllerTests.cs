using FPS.Booking.API.Controllers;
using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
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
