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

    // ── Failed draw: retried via workflow ─────────────────────────────────────

    [Fact]
    public async Task Handle_ExistingFailedAttempt_RestartsWorkflowAndReturnsInProgress()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto { Status = "Failed" });

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("InProgress", result.Status);
        Assert.False(result.WasAlreadyCompleted);
        workflowStarter.Verify(s => s.StartAsync(It.IsAny<TriggerDrawCommand>(), It.IsAny<CancellationToken>()), Times.Once);
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

    private static TriggerDrawCommand ValidCommand() => new(
        TenantId: "tenant-1",
        LocationId: "loc-1",
        Date: DrawDate,
        TimeSlotStart: SlotStart,
        TimeSlotEnd: SlotEnd,
        Reason: "Scheduled draw");
}
