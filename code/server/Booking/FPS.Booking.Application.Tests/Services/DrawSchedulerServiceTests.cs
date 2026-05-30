using FPS.Booking.Application.Services;
using MediatR;

namespace FPS.Booking.Application.Tests.Services;

public sealed class DrawSchedulerServiceTests
{
    private static readonly DateOnly TargetDate = new(2026, 6, 3);

    private static DrawScheduleTarget DefaultTarget() => new()
    {
        TenantId = "tenant-1",
        LocationId = "loc-1",
        TimeSlotStart = TimeSpan.FromHours(9),
        TimeSlotEnd = TimeSpan.FromHours(17)
    };

    private static TriggerDrawResult CompletedResult(string key) =>
        new(key, "Completed", 3, 1, 0, WasAlreadyCompleted: false);

    private static TriggerDrawResult InProgressResult(string key) =>
        new(key, "InProgress", 0, 0, 0, WasAlreadyCompleted: false);

    private static TriggerDrawResult AlreadyCompletedResult(string key) =>
        new(key, "Completed", 3, 1, 0, WasAlreadyCompleted: true);

    private static TriggerDrawResult FailedResult(string key) =>
        new(key, "Failed", 0, 0, 0, WasAlreadyCompleted: false);

    // ── Disabled scheduler ────────────────────────────────────────────────────

    [Fact]
    public async Task TriggerDueDrawsAsync_WhenDisabled_ReturnsDisabledForEachTarget()
    {
        var options = new DrawSchedulerOptions { Enabled = false, Targets = [DefaultTarget()] };
        var mediator = new Mock<IMediator>();
        var svc = new DrawSchedulerService(options, mediator.Object, NullLogger<DrawSchedulerService>.Instance);

        var results = await svc.TriggerDueDrawsAsync(TargetDate);

        Assert.Single(results);
        Assert.Equal("Disabled", results[0].Status);
        mediator.Verify(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TriggerDueDrawsAsync_WhenNoTargets_ReturnsEmpty()
    {
        var options = new DrawSchedulerOptions { Enabled = true, Targets = [] };
        var mediator = new Mock<IMediator>();
        var svc = new DrawSchedulerService(options, mediator.Object, NullLogger<DrawSchedulerService>.Instance);

        var results = await svc.TriggerDueDrawsAsync(TargetDate);

        Assert.Empty(results);
        mediator.Verify(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TriggerDueDrawsAsync_SingleTarget_SendsCommandWithCorrectDateAndSlot()
    {
        var options = new DrawSchedulerOptions { Enabled = true, Targets = [DefaultTarget()] };
        var mediator = new Mock<IMediator>();
        TriggerDrawCommand? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<TriggerDrawResult>, CancellationToken>((cmd, _) => captured = (TriggerDrawCommand)cmd)
            .ReturnsAsync(InProgressResult("draw:tenant-1:loc-1:2026-06-03:0900-1700"));
        var svc = new DrawSchedulerService(options, mediator.Object, NullLogger<DrawSchedulerService>.Instance);

        var results = await svc.TriggerDueDrawsAsync(TargetDate);

        Assert.Single(results);
        Assert.Equal("InProgress", results[0].Status);
        Assert.Equal("tenant-1", captured!.TenantId);
        Assert.Equal("loc-1", captured.LocationId);
        Assert.Equal(TargetDate, captured.Date);
        Assert.Equal(9, captured.TimeSlotStart.Hour);
        Assert.Equal(17, captured.TimeSlotEnd.Hour);
    }

    [Fact]
    public async Task TriggerDueDrawsAsync_SingleTarget_ReturnsStartedStatus()
    {
        var options = new DrawSchedulerOptions { Enabled = true, Targets = [DefaultTarget()] };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InProgressResult("draw:tenant-1:loc-1:2026-06-03:0900-1700"));
        var svc = new DrawSchedulerService(options, mediator.Object, NullLogger<DrawSchedulerService>.Instance);

        var results = await svc.TriggerDueDrawsAsync(TargetDate);

        Assert.Equal("InProgress", results[0].Status);
    }

    // ── Multi-instance safety ─────────────────────────────────────────────────

    [Fact]
    public async Task TriggerDueDrawsAsync_DuplicateTick_ReturnsAlreadyCompletedWithoutRetrigger()
    {
        // Simulates a second replica receiving the same cron tick after draw is already done.
        var options = new DrawSchedulerOptions { Enabled = true, Targets = [DefaultTarget()] };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AlreadyCompletedResult("draw:tenant-1:loc-1:2026-06-03:0900-1700"));
        var svc = new DrawSchedulerService(options, mediator.Object, NullLogger<DrawSchedulerService>.Instance);

        var results = await svc.TriggerDueDrawsAsync(TargetDate);

        Assert.Equal("AlreadyCompleted", results[0].Status);
        // TriggerDrawHandler was still called once — multi-instance safety comes from inside the handler.
        mediator.Verify(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriggerDueDrawsAsync_DrawAlreadyInProgress_ReturnsInProgress()
    {
        // Simulates two replicas both getting the cron tick: first starts, second sees InProgress.
        var options = new DrawSchedulerOptions { Enabled = true, Targets = [DefaultTarget()] };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InProgressResult("draw:tenant-1:loc-1:2026-06-03:0900-1700"));
        var svc = new DrawSchedulerService(options, mediator.Object, NullLogger<DrawSchedulerService>.Instance);

        var results = await svc.TriggerDueDrawsAsync(TargetDate);

        Assert.Equal("InProgress", results[0].Status);
    }

    // ── Failed draw — no silent restart ──────────────────────────────────────

    [Fact]
    public async Task TriggerDueDrawsAsync_DrawPreviouslyFailed_ReturnsFailed_NoRestart()
    {
        // TriggerDrawHandler returns "Failed" for existing failed draws (DRAW002 Codex fix).
        // The scheduler must surface this — not silently restart.
        var options = new DrawSchedulerOptions { Enabled = true, Targets = [DefaultTarget()] };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FailedResult("draw:tenant-1:loc-1:2026-06-03:0900-1700"));
        var svc = new DrawSchedulerService(options, mediator.Object, NullLogger<DrawSchedulerService>.Instance);

        var results = await svc.TriggerDueDrawsAsync(TargetDate);

        Assert.Equal("Failed", results[0].Status);
        // Called exactly once — handler returned Failed, scheduler did not retry.
        mediator.Verify(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Multiple targets ──────────────────────────────────────────────────────

    [Fact]
    public async Task TriggerDueDrawsAsync_MultipleTargets_SendsCommandForEach()
    {
        var target2 = new DrawScheduleTarget
        {
            TenantId = "tenant-2", LocationId = "loc-2",
            TimeSlotStart = TimeSpan.FromHours(8), TimeSlotEnd = TimeSpan.FromHours(18)
        };
        var options = new DrawSchedulerOptions { Enabled = true, Targets = [DefaultTarget(), target2] };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TriggerDrawCommand cmd, CancellationToken _) =>
                InProgressResult($"draw:{cmd.TenantId}:{cmd.LocationId}:{cmd.Date:yyyy-MM-dd}"));
        var svc = new DrawSchedulerService(options, mediator.Object, NullLogger<DrawSchedulerService>.Instance);

        var results = await svc.TriggerDueDrawsAsync(TargetDate);

        Assert.Equal(2, results.Count);
        mediator.Verify(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TriggerDueDrawsAsync_OneTargetThrows_OtherTargetsStillProcessed()
    {
        var target2 = new DrawScheduleTarget
        {
            TenantId = "tenant-2", LocationId = "loc-2",
            TimeSlotStart = TimeSpan.FromHours(8), TimeSlotEnd = TimeSpan.FromHours(18)
        };
        var options = new DrawSchedulerOptions { Enabled = true, Targets = [DefaultTarget(), target2] };
        var mediator = new Mock<IMediator>();
        mediator
            .SetupSequence(m => m.Send(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient error"))
            .ReturnsAsync(InProgressResult("draw:tenant-2:loc-2:2026-06-03"));
        var svc = new DrawSchedulerService(options, mediator.Object, NullLogger<DrawSchedulerService>.Instance);

        var results = await svc.TriggerDueDrawsAsync(TargetDate);

        Assert.Equal(2, results.Count);
        Assert.Equal("Failed", results[0].Status);
        Assert.Equal("InProgress", results[1].Status);
    }
}
