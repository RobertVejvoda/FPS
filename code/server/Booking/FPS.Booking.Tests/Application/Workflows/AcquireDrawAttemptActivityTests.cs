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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AcquireDrawAttemptInput MakeInput(string triggerSource = "manual") => new(
        DrawKey: DrawKey,
        TenantId: "tenant-1",
        LocationId: "loc-1",
        Date: "2026-06-02",
        TimeSlotStart: new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc).ToString("O"),
        TimeSlotEnd: new DateTime(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc).ToString("O"),
        Seed: 42L,
        TriggerSource: triggerSource,
        TriggeredBy: "hr-admin");
}
