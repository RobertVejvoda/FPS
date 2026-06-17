using FPS.Booking.Application.Workflows.Activities;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPS.Booking.Application.Tests.Workflows;

/// <summary>
/// Regression tests for DRAW009 review finding:
/// raw DiagnosticMessage / exception text must never be persisted into
/// DrawLifecycleStepRecord.ErrorMessage.  GET /draws/{date}/lifecycle
/// returns ErrorMessage to hr_manager, admin, and auditor callers, so
/// Dapr draw state is not a safe place for internal diagnostic text.
/// </summary>
public sealed class FailDrawAttemptActivityTests
{
    private readonly Mock<IDrawRepository> drawRepo = new();
    private readonly Mock<IBookingEventPublisher> eventPublisher = new();
    private readonly FailDrawAttemptActivity activity;

    private const string DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900-1700";
    private const string SafeMessage = "Draw workflow execution failed.";
    private const string RawDiagnostic = "NullReferenceException: Object reference not set to an instance of an object. at FPS.Booking.Allocator.Run() stack trace…";

    public FailDrawAttemptActivityTests()
    {
        activity = new FailDrawAttemptActivity(
            drawRepo.Object,
            eventPublisher.Object,
            NullLogger<FailDrawAttemptActivity>.Instance);

        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);
        drawRepo.Setup(r => r.SaveAsync(It.IsAny<DrawAttemptDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        eventPublisher.Setup(p => p.WithContext(It.IsAny<BookingPublishContext>()))
            .Returns(eventPublisher.Object);
        eventPublisher.Setup(p => p.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task RunAsync_WithDiagnosticMessage_StoresOnlySafeMessageInLifecycleStep()
    {
        // Arrange – input carries both the internal diagnostic and the safe public message
        var input = MakeInput(safeErrorMessage: SafeMessage, diagnosticMessage: RawDiagnostic);

        DrawAttemptDto? saved = null;
        drawRepo.Setup(r => r.SaveAsync(It.IsAny<DrawAttemptDto>(), It.IsAny<CancellationToken>()))
            .Callback<DrawAttemptDto, CancellationToken>((dto, _) => saved = dto)
            .Returns(Task.CompletedTask);

        // Act
        await activity.RunAsync(null!, input);

        // Assert – only the safe message must be in state; raw diagnostic must not appear
        Assert.NotNull(saved);
        var failStep = saved!.LifecycleSteps.Single(s => s.StepName == "DrawFailed");
        Assert.Equal(SafeMessage, failStep.ErrorMessage);
        Assert.DoesNotContain(RawDiagnostic, failStep.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task RunAsync_WithDiagnosticMessage_DoesNotPublishRawDiagnosticToDataHub()
    {
        // Arrange
        var input = MakeInput(safeErrorMessage: SafeMessage, diagnosticMessage: RawDiagnostic);

        DrawAttemptFailedEvent? published = null;
        eventPublisher.Setup(p => p.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) => published = e as DrawAttemptFailedEvent)
            .Returns(Task.CompletedTask);

        // Act
        await activity.RunAsync(null!, input);

        // Assert – DataHub receives the safe failure reason, not internal exception text
        Assert.NotNull(published);
        Assert.Equal(SafeMessage, published!.SafeFailureReason);
        Assert.DoesNotContain(RawDiagnostic, published.SafeFailureReason ?? string.Empty);
    }

    [Fact]
    public async Task RunAsync_WithoutDiagnosticMessage_StoresSafeMessageInLifecycleStep()
    {
        // Arrange – no diagnostic; SafeErrorMessage is the only message available
        var input = MakeInput(safeErrorMessage: SafeMessage, diagnosticMessage: null);

        DrawAttemptDto? saved = null;
        drawRepo.Setup(r => r.SaveAsync(It.IsAny<DrawAttemptDto>(), It.IsAny<CancellationToken>()))
            .Callback<DrawAttemptDto, CancellationToken>((dto, _) => saved = dto)
            .Returns(Task.CompletedTask);

        // Act
        await activity.RunAsync(null!, input);

        // Assert
        Assert.NotNull(saved);
        var failStep = saved!.LifecycleSteps.Single(s => s.StepName == "DrawFailed");
        Assert.Equal(SafeMessage, failStep.ErrorMessage);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FailDrawAttemptInput MakeInput(
        string safeErrorMessage,
        string? diagnosticMessage) => new(
            DrawKey: DrawKey,
            TenantId: "tenant-1",
            LocationId: "loc-1",
            Date: "2026-06-02",
            Seed: 42L,
            StartedAt: new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc).ToString("O"),
            SafeErrorMessage: safeErrorMessage,
            TimeSlotStart: new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc).ToString("O"),
            TimeSlotEnd: new DateTime(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc).ToString("O"),
            TriggerSource: "scheduled",
            TriggeredBy: null,
            Reason: null,
            DiagnosticMessage: diagnosticMessage);
}
