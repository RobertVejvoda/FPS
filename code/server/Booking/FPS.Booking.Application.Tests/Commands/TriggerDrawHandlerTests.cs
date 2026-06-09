using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Workflows;

namespace FPS.Booking.Application.Tests.Commands;

public sealed class TriggerDrawHandlerTests
{
    private readonly Mock<IDrawRepository> drawRepo = new();
    private readonly Mock<IDrawWorkflowStarter> workflowStarter = new();
    private readonly TriggerDrawHandler handler;

    private static readonly DateOnly DrawDate = new(2026, 6, 2);
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    public TriggerDrawHandlerTests()
    {
        handler = new TriggerDrawHandler(drawRepo.Object, workflowStarter.Object);

        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        workflowStarter
            .Setup(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawStartResult("draw:tenant-1:loc-1:2026-06-02:0900", "draw:tenant-1:loc-1:2026-06-02:0900", "Started"));
    }

    // ── Happy path: new draw starts workflow ──────────────────────────────────

    [Fact]
    public async Task Handle_NoExistingAttempt_StartsWorkflowAndReturnsInProgress()
    {
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("InProgress", result.Status);
        Assert.False(result.WasAlreadyCompleted);
        workflowStarter.Verify(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Duplicate start: already running ─────────────────────────────────────

    [Fact]
    public async Task Handle_ExistingInProgressAttempt_ReturnsInProgressWithoutStartingWorkflow()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto { Status = "InProgress" });

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("InProgress", result.Status);
        Assert.False(result.WasAlreadyCompleted);
        workflowStarter.Verify(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Existing completed draw ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExistingCompletedAttempt_ReturnsCachedResultWithoutStartingWorkflow()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto
            {
                DrawKey = "existing-key",
                Status = "Completed",
                AllocatedCount = 3,
                RejectedCount = 1,
                WaitlistedCount = 2,
            });

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.WasAlreadyCompleted);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(3, result.AllocatedCount);
        Assert.Equal(1, result.RejectedCount);
        workflowStarter.Verify(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Failed draw: surfaces Failed without recovery by default ─────────────

    [Fact]
    public async Task Handle_ExistingFailedAttempt_ReturnsFailedWithoutStartingWorkflow()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto { Status = "Failed" });

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.False(result.WasAlreadyCompleted);
        workflowStarter.Verify(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Failed draw with explicit recovery ────────────────────────────────────

    [Fact]
    public async Task Handle_ExistingFailedAttemptWithRecovery_ArchivesAndStartsNewWorkflow()
    {
        var failedAttempt = new DrawAttemptDto
        {
            DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900",
            Status = "Failed",
            LifecycleSteps = []
        };

        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedAttempt);

        var cmd = ValidCommand() with { AllowRecovery = true, Reason = "Manual recovery by admin" };
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("InProgress", result.Status);
        Assert.False(result.WasAlreadyCompleted);

        // Verify failed attempt was archived
        drawRepo.Verify(r => r.SaveAsync(
            It.Is<DrawAttemptDto>(a => a.Status == "FailedArchived" && a.LifecycleSteps.Any(s => s.StepName == "RecoveryInitiated")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify new workflow started with recovery trigger source
        workflowStarter.Verify(s => s.StartAsync(
            It.Is<TriggerDrawCommand>(c => c.TriggerSource == "recovery"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Completed draw immutability ───────────────────────────────────────────

    [Fact]
    public async Task Handle_CompletedDraw_CannotBeRerun_EvenWithRecovery()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto
            {
                Status = "Completed",
                AllocatedCount = 5,
                RejectedCount = 2,
                WaitlistedCount = 1,
            });

        var cmdWithRecovery = ValidCommand() with { AllowRecovery = true };
        var result = await handler.Handle(cmdWithRecovery, CancellationToken.None);

        Assert.Equal("Completed", result.Status);
        Assert.True(result.WasAlreadyCompleted);
        workflowStarter.Verify(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Draw key is included in the result ───────────────────────────────────

    [Fact]
    public async Task Handle_NoExistingAttempt_DrawKeyIncludedInResult()
    {
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.NotNull(result.DrawAttemptId);
        Assert.Contains("tenant-1", result.DrawAttemptId);
        Assert.Contains("loc-1", result.DrawAttemptId);
    }

    // ── Recovery: distinct workflow instance ID avoids AlreadyRunning collision ─

    [Fact]
    public async Task Handle_FailedDraw_WithAllowRecovery_ArchivesAndStartsWithDistinctInstanceId()
    {
        var failedAttempt = new DrawAttemptDto
        {
            DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900-1700",
            Status = "Failed",
            LifecycleSteps = []
        };
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedAttempt);

        TriggerDrawCommand? capturedCmd = null;
        workflowStarter
            .Setup(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()))
            .Callback<TriggerDrawCommand, CancellationToken>((c, _) => capturedCmd = c)
            .ReturnsAsync(new DrawStartResult("key", "recovery-instance", "Started"));

        var result = await handler.Handle(ValidCommand() with { AllowRecovery = true }, CancellationToken.None);

        Assert.Equal("InProgress", result.Status);

        // Archived with recovery step
        drawRepo.Verify(r => r.SaveAsync(
            It.Is<DrawAttemptDto>(a =>
                a.Status == "FailedArchived" &&
                a.LifecycleSteps.Any(s => s.StepName == "RecoveryInitiated")),
            It.IsAny<CancellationToken>()), Times.Once);

        // New workflow started with TriggerSource=recovery and a distinct instance ID
        Assert.NotNull(capturedCmd);
        Assert.Equal("recovery", capturedCmd!.TriggerSource);
        Assert.NotNull(capturedCmd.WorkflowInstanceIdOverride);
        Assert.Contains("-r-", capturedCmd.WorkflowInstanceIdOverride);

        // Instance ID must differ from the draw key (avoids AlreadyRunning collision)
        var drawKey = result.DrawAttemptId;
        Assert.NotEqual(drawKey, capturedCmd.WorkflowInstanceIdOverride);
    }

    [Fact]
    public async Task Handle_FailedDraw_WithoutAllowRecovery_ReturnsFailedAndDoesNotStart()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto { Status = "Failed" });

        var result = await handler.Handle(ValidCommand() with { AllowRecovery = false }, CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        drawRepo.Verify(r => r.SaveAsync(It.IsAny<DrawAttemptDto>(), It.IsAny<CancellationToken>()), Times.Never);
        workflowStarter.Verify(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CompletedDraw_IsImmutableEvenWithAllowRecovery()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto { Status = "Completed", AllocatedCount = 4 });

        var result = await handler.Handle(ValidCommand() with { AllowRecovery = true }, CancellationToken.None);

        Assert.Equal("Completed", result.Status);
        Assert.True(result.WasAlreadyCompleted);
        workflowStarter.Verify(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TriggerDrawCommand ValidCommand() => new(
        TenantId: "tenant-1",
        LocationId: "loc-1",
        Date: DrawDate,
        TimeSlotStart: SlotStart,
        TimeSlotEnd: SlotEnd,
        Reason: "Scheduled draw");
}
