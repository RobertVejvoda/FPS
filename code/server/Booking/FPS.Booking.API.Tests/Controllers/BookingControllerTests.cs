using FPS.Booking.API.Controllers;
using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Booking.API.Tests.Controllers;

public class BookingControllerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly BookingController _controller;

    public BookingControllerTests()
    {
        _controller = new BookingController(_mediator.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task SubmitBookingRequest_ValidRequest_Returns202Accepted()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SubmitBookingRequestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitBookingRequestResult(Guid.NewGuid(), "Pending", null, null));

        var result = await _controller.SubmitBookingRequest(
            ValidBody(), "tenant-1", Guid.NewGuid().ToString(), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var body = Assert.IsType<SubmitBookingResponse>(accepted.Value);
        Assert.Equal("Pending", body.Status);
    }

    [Fact]
    public async Task SubmitBookingRequest_DuplicateRequest_Returns422()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SubmitBookingRequestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitBookingRequestResult(
                Guid.NewGuid(), "Rejected", "DuplicateRequest",
                "You already have a request for an overlapping time slot."));

        var result = await _controller.SubmitBookingRequest(
            ValidBody(), "tenant-1", Guid.NewGuid().ToString(), CancellationToken.None);

        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var body = Assert.IsType<SubmitBookingResponse>(unprocessable.Value);
        Assert.Equal("Rejected", body.Status);
        Assert.Equal("DuplicateRequest", body.RejectionCode);
    }

    [Fact]
    public async Task SubmitBookingRequest_MapsCommandFieldsCorrectly()
    {
        SubmitBookingRequestCommand? captured = null;
        _mediator
            .Setup(m => m.Send(It.IsAny<SubmitBookingRequestCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<SubmitBookingRequestResult>, CancellationToken>((cmd, _) => captured = (SubmitBookingRequestCommand)cmd)
            .ReturnsAsync(new SubmitBookingRequestResult(Guid.NewGuid(), "Pending", null, null));

        var body = ValidBody();
        await _controller.SubmitBookingRequest(body, "tenant-42", "user-99", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("tenant-42", captured.TenantId);
        Assert.Equal("user-99", captured.RequestorId);
        Assert.Equal(body.FacilityId, captured.FacilityId);
        Assert.Equal(body.LicensePlate, captured.LicensePlate);
    }

    private static SubmitBookingRequest ValidBody() => new(
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
