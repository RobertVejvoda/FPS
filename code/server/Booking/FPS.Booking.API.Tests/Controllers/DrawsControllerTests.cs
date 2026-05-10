using FPS.Booking.API.Controllers;
using FPS.Booking.API.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
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

    // ── GET /draws/{date}/status ──────────────────────────────────────────────

    [Fact]
    public async Task GetDrawStatus_CompletedDraw_Returns200()
    {
        mediator.Setup(m => m.Send(It.IsAny<GetDrawStatusQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawStatusResult(
                "draw-key", "tenant-1", "loc-1", DrawDate,
                "Completed", 5, 3, 1, 1, 0, [], "1.0", 42, "draw-key",
                DateTime.UtcNow, DateTime.UtcNow));

        var result = await controller.GetDrawStatus(
            DrawDate, "loc-1", SlotStart, SlotEnd, "tenant-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<DrawStatusResponse>(ok.Value);
        Assert.Equal("Completed", body.Status);
        Assert.Equal(3, body.AllocatedCount);
        Assert.Equal(42, body.Seed);
    }

    [Fact]
    public async Task GetDrawStatus_NoDraw_Returns404()
    {
        mediator.Setup(m => m.Send(It.IsAny<GetDrawStatusQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawStatusResult?)null);

        var result = await controller.GetDrawStatus(
            DrawDate, "loc-1", SlotStart, SlotEnd, "tenant-1", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static TriggerDrawRequest ValidBody() => new(
        LocationId: "loc-1",
        Date: DrawDate,
        TimeSlotStart: SlotStart,
        TimeSlotEnd: SlotEnd,
        Reason: "Scheduled draw");
}
