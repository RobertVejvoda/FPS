using FPS.Booking.Controllers;
using FPS.Booking.Models;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.SharedKernel.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Booking.Tests.Controllers;

public sealed class DrawsControllerTests
{
    private readonly Mock<IMediator> mediator = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly DrawsController controller;

    private static readonly DateOnly DrawDate = new(2026, 6, 2);
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    public DrawsControllerTests()
    {
        currentUser.Setup(u => u.TenantId).Returns("tenant-1");
        currentUser.Setup(u => u.UserId).Returns("hr-user-1");
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);

        controller = new DrawsController(mediator.Object, currentUser.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public async Task TriggerDraw_NewDraw_Returns202Accepted()
    {
        mediator.Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriggerDrawResult("draw-key", "Completed", 3, 1, 2, WasAlreadyCompleted: false));

        var result = await controller.TriggerDraw(ValidBody(), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var body = Assert.IsType<TriggerDrawResponse>(accepted.Value);
        Assert.Equal(3, body.AllocatedCount);
    }

    [Fact]
    public async Task TriggerDraw_AlreadyCompleted_Returns200Ok()
    {
        mediator.Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriggerDrawResult("draw-key", "Completed", 3, 1, 2, WasAlreadyCompleted: true));

        var result = await controller.TriggerDraw(ValidBody(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task TriggerDraw_MapsTenantFromCurrentUser()
    {
        currentUser.Setup(u => u.TenantId).Returns("tenant-99");

        TriggerDrawCommand? captured = null;
        mediator.Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<TriggerDrawResult>, CancellationToken>((cmd, _) => captured = (TriggerDrawCommand)cmd)
            .ReturnsAsync(new TriggerDrawResult("k", "Completed", 0, 0, 0, false));

        await controller.TriggerDraw(ValidBody(), CancellationToken.None);

        Assert.Equal("tenant-99", captured?.TenantId);
    }

    [Fact]
    public async Task TriggerDraw_FailedDraw_Returns202WithSafeStatus()
    {
        mediator.Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriggerDrawResult("draw-key", "Failed", 0, 0, 0, WasAlreadyCompleted: false));

        var result = await controller.TriggerDraw(ValidBody(), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var body = Assert.IsType<TriggerDrawResponse>(accepted.Value);
        Assert.Equal("Failed", body.Status);
    }

    [Fact]
    public async Task TriggerDraw_AllowRecovery_PassesFlagToCommand()
    {
        TriggerDrawCommand? captured = null;
        mediator.Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<TriggerDrawResult>, CancellationToken>((cmd, _) => captured = (TriggerDrawCommand)cmd)
            .ReturnsAsync(new TriggerDrawResult("draw-key", "InProgress", 0, 0, 0, WasAlreadyCompleted: false));

        var body = ValidBody() with { AllowRecovery = true };
        await controller.TriggerDraw(body, CancellationToken.None);

        Assert.True(captured?.AllowRecovery);
    }

    [Fact]
    public async Task TriggerDraw_PassesAuthenticatedUserAsTriggeredBy()
    {
        // Codex review on PR #492: the runner identity must come from the
        // authenticated HR/admin, not the misleading "hr-admin" default
        // that used to ship on every manual run.
        currentUser.Setup(u => u.UserId).Returns("hr-alice-real");

        TriggerDrawCommand? captured = null;
        mediator.Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<TriggerDrawResult>, CancellationToken>((cmd, _) => captured = (TriggerDrawCommand)cmd)
            .ReturnsAsync(new TriggerDrawResult("k", "InProgress", 0, 0, 0, WasAlreadyCompleted: false));

        await controller.TriggerDraw(ValidBody(), CancellationToken.None);

        Assert.Equal("hr-alice-real", captured?.TriggeredBy);
    }

    [Fact]
    public async Task TriggerDraw_UnauthenticatedUserId_Returns401()
    {
        // Defence in depth: even if the role gate is somehow bypassed,
        // we never publish a draw event without a real runner identity.
        currentUser.Setup(u => u.UserId).Returns(string.Empty);

        var result = await controller.TriggerDraw(ValidBody(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    // ── GET /draws/{date}/status ──────────────────────────────────────────────

    [Fact]
    public async Task GetDrawStatus_CompletedDraw_Returns200WithEmployeeSafeFields()
    {
        var started = new DateTime(2026, 6, 2, 18, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2026, 6, 2, 18, 0, 5, DateTimeKind.Utc);
        mediator.Setup(m => m.Send(It.IsAny<GetDrawStatusQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawStatusResult(
                "draw-key", "tenant-1", "loc-1", DrawDate,
                "Completed", 5, 3, 1, 1, 0, [], "1.0", 42, "draw-key",
                started, completed, "Low",
                CutOffAt: "2026-06-02T18:00:00+00:00", NextDrawAt: null, TimeZone: "UTC",
                RequestWindowStatus: "closed", ScheduleStatus: "known", ScheduleSource: "tenantPolicy",
                LastCalculatedAt: completed, SafeMessage: "Spot allocation is complete."));

        var result = await controller.GetDrawStatus(DrawDate, "loc-1", SlotStart, SlotEnd, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<DrawStatusResponse>(ok.Value);
        Assert.Equal("Completed", body.Status);
        Assert.Equal("Low", body.DemandLevel);
        Assert.Equal(started, body.StartedAt);
        Assert.Equal(completed, body.CompletedAt);
        Assert.Equal(5, body.RequestCount);
        Assert.True(body.CanRequest);
        Assert.Null(body.CannotRequestReason);
    }

    [Fact]
    public async Task GetDrawStatus_NoDraw_Returns200WithPreDrawDefault()
    {
        mediator.Setup(m => m.Send(It.IsAny<GetDrawStatusQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawStatusResult(
                "draw-key", "tenant-1", "loc-1", DrawDate,
                "NotScheduled", 0, 0, 0, 0, 0, [], string.Empty, 0, null,
                null, null, "Unknown",
                CutOffAt: "2026-06-02T18:00:00+00:00", NextDrawAt: null, TimeZone: "UTC",
                RequestWindowStatus: "open", ScheduleStatus: "known", ScheduleSource: "tenantPolicy",
                LastCalculatedAt: DateTime.UtcNow, SafeMessage: "Requests are open until 18:00 (UTC).",
                CanRequest: true));

        var result = await controller.GetDrawStatus(DrawDate, "loc-1", SlotStart, SlotEnd, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<DrawStatusResponse>(ok.Value);
        Assert.Equal("NotScheduled", body.Status);
        Assert.Equal("Unknown", body.DemandLevel);
        Assert.Equal(0, body.RequestCount);
        Assert.True(body.CanRequest);
    }

    // ── GET /draws/{date}/lifecycle – missing time params (#561) ─────────────

    [Theory]
    [InlineData(true, false)]   // start provided, end omitted
    [InlineData(false, true)]   // start omitted,  end provided
    [InlineData(false, false)]  // both omitted
    public async Task GetDrawLifecycle_MissingTimeSlotParams_Returns400(bool hasStart, bool hasEnd)
    {
        var start = hasStart ? SlotStart : default;
        var end   = hasEnd   ? SlotEnd   : default;

        var result = await controller.GetDrawLifecycle(DrawDate, "loc-1", start, end, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mediator.Verify(m => m.Send(It.IsAny<GetDrawLifecycleQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDrawLifecycle_BothParamsProvided_SendsQuery()
    {
        mediator.Setup(m => m.Send(It.IsAny<GetDrawLifecycleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawLifecycleResult?)null);

        var result = await controller.GetDrawLifecycle(DrawDate, "loc-1", SlotStart, SlotEnd, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        mediator.Verify(m => m.Send(It.IsAny<GetDrawLifecycleQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GET /draws/{date}/status – missing time params (#561) ────────────────

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task GetDrawStatus_MissingTimeSlotParams_Returns400(bool hasStart, bool hasEnd)
    {
        var start = hasStart ? SlotStart : default;
        var end   = hasEnd   ? SlotEnd   : default;

        var result = await controller.GetDrawStatus(DrawDate, "loc-1", start, end, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mediator.Verify(m => m.Send(It.IsAny<GetDrawStatusQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TriggerDrawRequest ValidBody() => new(
        LocationId: "loc-1",
        Date: DrawDate,
        TimeSlotStart: SlotStart,
        TimeSlotEnd: SlotEnd,
        Reason: "Scheduled draw");
}
