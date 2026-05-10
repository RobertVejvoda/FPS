using FPS.Booking.API.Controllers;
using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Booking.API.Tests.Controllers;

public sealed class DrawsControllerTests
{
    private readonly Mock<IMediator> mediator = new();
    private readonly DrawsController controller;

    private static readonly DateOnly DrawDate = new(2026, 6, 2);
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    public DrawsControllerTests()
    {
        controller = new DrawsController(mediator.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public async Task TriggerDraw_NewDraw_Returns202Accepted()
    {
        mediator.Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriggerDrawResult("draw-key", "Completed", 3, 1, 2, WasAlreadyCompleted: false));

        var result = await controller.TriggerDraw(ValidBody(), "tenant-1", CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var body = Assert.IsType<TriggerDrawResponse>(accepted.Value);
        Assert.Equal(3, body.AllocatedCount);
    }

    [Fact]
    public async Task TriggerDraw_AlreadyCompleted_Returns200Ok()
    {
        mediator.Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriggerDrawResult("draw-key", "Completed", 3, 1, 2, WasAlreadyCompleted: true));

        var result = await controller.TriggerDraw(ValidBody(), "tenant-1", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task TriggerDraw_MapsTenantFromHeader()
    {
        TriggerDrawCommand? captured = null;
        mediator.Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<TriggerDrawResult>, CancellationToken>((cmd, _) => captured = (TriggerDrawCommand)cmd)
            .ReturnsAsync(new TriggerDrawResult("k", "Completed", 0, 0, 0, false));

        await controller.TriggerDraw(ValidBody(), "tenant-99", CancellationToken.None);

        Assert.Equal("tenant-99", captured?.TenantId);
    }

    private static TriggerDrawRequest ValidBody() => new(
        LocationId: "loc-1",
        Date: DrawDate,
        TimeSlotStart: SlotStart,
        TimeSlotEnd: SlotEnd,
        Reason: "Scheduled draw");
}
