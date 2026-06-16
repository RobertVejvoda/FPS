using Dapr.Workflow;
using FPS.Booking.Application.Workflows.Activities;
using FPS.Booking.Domain.Events;
using FPS.SharedKernel.DomainEvents;

namespace FPS.Booking.Application.Tests.Workflows;

public sealed class AcquireDrawAttemptActivityTests
{
    private readonly Mock<IDrawRepository> drawRepo = new();
    private readonly Mock<IBookingEventPublisher> eventPublisher = new();
    private readonly AcquireDrawAttemptActivity activity;

    private const string DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900-1700";

    public AcquireDrawAttemptActivityTests()
    {
        activity = new AcquireDrawAttemptActivity(drawRepo.Object, eventPublisher.Object);

        drawRepo.Setup(r => r.SaveAsync(It.IsAny<DrawAttemptDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        eventPublisher.Setup(p => p.WithContext(It.IsAny<BookingPublishContext>()))
            .Returns(eventPublisher.Object);
        eventPublisher.Setup(p => p.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Fresh attempt: single Scheduled lifecycle step ────────────────────────

    [Fact]
    public async Task RunAsync_NoExistingAttempt_SavesInProgressWithScheduledStep()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(DrawKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        await activity.RunAsync(null!, MakeInput());

        drawRepo.Verify(r => r.SaveAsync(
            It.Is<DrawAttemptDto>(a =>
                a.Status == "InProgress" &&
                a.LifecycleSteps.Count == 1 &&
                a.LifecycleSteps[0].StepName == "Scheduled"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Recovery: archived lifecycle steps are carried forward ────────────────

    [Fact]
    public async Task RunAsync_FailedArchivedAttempt_CarriesForwardAuditTrailAndAddsScheduledStep()
    {
        var archivedAttempt = new DrawAttemptDto
        {
            DrawKey = DrawKey,
            Status = "FailedArchived",
            LifecycleSteps =
            [
                new DrawLifecycleStepRecord { StepName = "Scheduled", Status = "Completed", StartedAt = DateTime.UtcNow.AddMinutes(-10), CompletedAt = DateTime.UtcNow.AddMinutes(-10), Summary = "original" },
                new DrawLifecycleStepRecord { StepName = "RecoveryInitiated", Status = "Completed", StartedAt = DateTime.UtcNow.AddSeconds(-5), CompletedAt = DateTime.UtcNow.AddSeconds(-5), Summary = "Recovery triggered." },
            ]
        };
        drawRepo.Setup(r => r.GetByKeyAsync(DrawKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(archivedAttempt);

        await activity.RunAsync(null!, MakeInput(triggerSource: "recovery"));

        drawRepo.Verify(r => r.SaveAsync(
            It.Is<DrawAttemptDto>(a =>
                a.Status == "InProgress" &&
                a.LifecycleSteps.Any(s => s.StepName == "RecoveryInitiated") &&
                a.LifecycleSteps.Any(s => s.StepName == "Scheduled" && s.Summary!.Contains("recovery")) &&
                a.LifecycleSteps.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_FailedArchivedAttempt_ReturnsNotAlreadyRunning()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(DrawKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto
            {
                DrawKey = DrawKey,
                Status = "FailedArchived",
                LifecycleSteps = [new DrawLifecycleStepRecord { StepName = "RecoveryInitiated", Status = "Completed" }]
            });

        var output = await activity.RunAsync(null!, MakeInput(triggerSource: "recovery"));

        Assert.False(output.WasAlreadyRunning);
    }

    // ── Duplicate trigger: InProgress returns without overwriting ─────────────

    [Fact]
    public async Task RunAsync_ExistingInProgressAttempt_ReturnsAlreadyRunningWithoutSaving()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(DrawKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto { DrawKey = DrawKey, Status = "InProgress", StartedAt = DateTime.UtcNow });

        var output = await activity.RunAsync(null!, MakeInput());

        Assert.True(output.WasAlreadyRunning);
        drawRepo.Verify(r => r.SaveAsync(It.IsAny<DrawAttemptDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Issue #472 / PR #492 review: runner identity on operator-triggered runs ──

    [Fact]
    public async Task RunAsync_ManualRun_PublishesEventWithHrActorAndReason()
    {
        BookingPublishContext? capturedCtx = null;
        DrawAttemptStartedEvent? capturedEvent = null;
        eventPublisher.Setup(p => p.WithContext(It.IsAny<BookingPublishContext>()))
            .Callback<BookingPublishContext>(ctx => capturedCtx = ctx)
            .Returns(eventPublisher.Object);
        eventPublisher.Setup(p => p.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) => capturedEvent = e as DrawAttemptStartedEvent)
            .Returns(Task.CompletedTask);
        drawRepo.Setup(r => r.GetByKeyAsync(DrawKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        await activity.RunAsync(null!, MakeInput(triggerSource: "manual", reason: "Cut-off reached early"));

        Assert.NotNull(capturedCtx);
        Assert.Equal("hr_manager", capturedCtx!.ActorType);
        Assert.Equal("hr-admin", capturedCtx.ActorId);
        Assert.NotNull(capturedEvent);
        Assert.Equal("manual", capturedEvent!.TriggerSource);
        Assert.Equal("Cut-off reached early", capturedEvent.RunReason);
        Assert.Equal("hr-admin", capturedEvent.TriggeredBy);
    }

    [Fact]
    public async Task RunAsync_RecoveryRun_PublishesEventWithHrActorAndReason()
    {
        // The Codex finding: recovery used to lose the runner because the
        // activity only treated "manual" as operator-triggered. Now any
        // non-scheduled source surfaces the actor.
        BookingPublishContext? capturedCtx = null;
        DrawAttemptStartedEvent? capturedEvent = null;
        eventPublisher.Setup(p => p.WithContext(It.IsAny<BookingPublishContext>()))
            .Callback<BookingPublishContext>(ctx => capturedCtx = ctx)
            .Returns(eventPublisher.Object);
        eventPublisher.Setup(p => p.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) => capturedEvent = e as DrawAttemptStartedEvent)
            .Returns(Task.CompletedTask);
        drawRepo.Setup(r => r.GetByKeyAsync(DrawKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        await activity.RunAsync(null!, MakeInput(triggerSource: "recovery", reason: "Retry after allocator failure"));

        Assert.Equal("hr_manager", capturedCtx?.ActorType);
        Assert.Equal("hr-admin", capturedCtx?.ActorId);
        Assert.Equal("recovery", capturedEvent?.TriggerSource);
        Assert.Equal("Retry after allocator failure", capturedEvent?.RunReason);
        Assert.Equal("hr-admin", capturedEvent?.TriggeredBy);
    }

    [Fact]
    public async Task RunAsync_ScheduledRun_PublishesEventWithSystemActor()
    {
        // Scheduled runs must keep the system actor — they don't have a
        // real user behind them, and the scheduler identity is not exposed
        // beyond the source label.
        BookingPublishContext? capturedCtx = null;
        eventPublisher.Setup(p => p.WithContext(It.IsAny<BookingPublishContext>()))
            .Callback<BookingPublishContext>(ctx => capturedCtx = ctx)
            .Returns(eventPublisher.Object);
        drawRepo.Setup(r => r.GetByKeyAsync(DrawKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        await activity.RunAsync(null!, MakeInput(triggerSource: "scheduled"));

        Assert.Equal("system", capturedCtx?.ActorType);
        Assert.Null(capturedCtx?.ActorId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AcquireDrawAttemptInput MakeInput(string triggerSource = "manual", string? reason = null) => new(
        DrawKey: DrawKey,
        TenantId: "tenant-1",
        LocationId: "loc-1",
        Date: "2026-06-02",
        TimeSlotStart: new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc).ToString("O"),
        TimeSlotEnd: new DateTime(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc).ToString("O"),
        Seed: 42L,
        TriggerSource: triggerSource,
        TriggeredBy: "hr-admin",
        Reason: reason);
}
