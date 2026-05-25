using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Services;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Tests.Commands;

public sealed class TriggerDrawHandlerTests
{
    private readonly Mock<IBookingRepository> bookingRepo = new();
    private readonly Mock<IBookingQueryRepository> bookingQueryRepo = new();
    private readonly Mock<IDrawRepository> drawRepo = new();
    private readonly Mock<IEmployeeMetricsService> metricsService = new();
    private readonly Mock<IAvailableSlotService> slotService = new();
    private readonly Mock<ITenantPolicyService> policyService = new();
    private readonly Mock<IBookingEventPublisher> publisher = new();
    private readonly TriggerDrawHandler handler;

    private static readonly TenantPolicy DefaultPolicy = new(500, new TimeOnly(18, 0), "UTC", true, 10);
    private static readonly DateOnly DrawDate = new(2026, 6, 2);
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    public TriggerDrawHandlerTests()
    {
        handler = new TriggerDrawHandler(
            bookingRepo.Object, bookingQueryRepo.Object, drawRepo.Object, metricsService.Object,
            slotService.Object, policyService.Object, publisher.Object, new DrawService());

        policyService.Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy);

        bookingQueryRepo.Setup(r => r.GetPendingRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookingRequestDto>());

        slotService.Setup(s => s.GetAvailableSlotsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
            It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        metricsService.Setup(m => m.GetMetricsSnapshotAsync(
            It.IsAny<string>(), It.IsAny<IEnumerable<string>>(),
            It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, EmployeeMetrics>());

        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        drawRepo.Setup(r => r.SaveAsync(It.IsAny<DrawAttemptDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher.Setup(p => p.WithContext(It.IsAny<BookingPublishContext>())).Returns(publisher.Object);
        publisher.Setup(p => p.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NoPendingRequests_ReturnsCompletedWithZeroCounts()
    {
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Completed", result.Status);
        Assert.Equal(0, result.AllocatedCount);
        Assert.Equal(0, result.RejectedCount);
        Assert.Equal(0, result.WaitlistedCount);
        Assert.False(result.WasAlreadyCompleted);
    }

    [Fact]
    public async Task Handle_PersistsDrawAttempt()
    {
        await handler.Handle(ValidCommand(), CancellationToken.None);

        drawRepo.Verify(r => r.SaveAsync(
            It.Is<DrawAttemptDto>(d => d.Status == "Completed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PublishesStartAndCompletedEvents()
    {
        await handler.Handle(ValidCommand(), CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.DrawAttemptStartedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);

        publisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.DrawAttemptCompletedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AlreadyCompletedDraw_ReturnsExistingWithoutReRunning()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto
            {
                DrawKey = "existing-key",
                Status = "Completed",
                AllocatedCount = 3,
                RejectedCount = 1,
                WaitlistedCount = 2
            });

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.WasAlreadyCompleted);
        Assert.Equal(3, result.AllocatedCount);
        drawRepo.Verify(r => r.SaveAsync(It.IsAny<DrawAttemptDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_DoesNotUpdateBookingStatuses()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawAttemptDto { Status = "Completed" });

        await handler.Handle(ValidCommand(), CancellationToken.None);

        bookingRepo.Verify(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Per-decision outcome events ───────────────────────────────────────────

    [Fact]
    public async Task Handle_DrawWaitlistsRequest_DoesNotPublishRejectedEvent()
    {
        // Regular (non-company-car) request with no available slots → Waitlisted, not Rejected.
        // No booking.requestRejected event should be published for waitlisted decisions.
        var pending = PendingDto();
        bookingQueryRepo.Setup(r => r.GetPendingRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending]);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(0, result.RejectedCount);
        Assert.Equal(1, result.WaitlistedCount);
        publisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.BookingRequestRejectedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DrawAllocatesRequest_PublishesSlotAllocatedEvent()
    {
        var pending = PendingDto();
        bookingQueryRepo.Setup(r => r.GetPendingRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending]);

        slotService.Setup(s => s.GetAvailableSlotsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
            It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("S1"))]);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.SlotAllocationCreatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static BookingRequestDto PendingDto() => new()
    {
        RequestId = Guid.NewGuid(),
        RequestedBy = Guid.NewGuid().ToString(),
        PlannedArrivalTime = SlotStart,
        PlannedDepartureTime = SlotEnd,
        RequestedAt = DateTime.UtcNow.AddHours(-1),
        Status = "Pending",
        LocationId = "loc-1",
    };

    private static TriggerDrawCommand ValidCommand() => new(
        TenantId: "tenant-1",
        LocationId: "loc-1",
        Date: DrawDate,
        TimeSlotStart: SlotStart,
        TimeSlotEnd: SlotEnd,
        Reason: "Scheduled draw");

    // ── AUD007: Draw Lifecycle Tracking ───────────────────────────────────────

    [Fact]
    public async Task Handle_CompletedDraw_CapturesAllLifecycleSteps()
    {
        var pending = PendingDto();
        bookingQueryRepo.Setup(r => r.GetPendingRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending]);

        slotService.Setup(s => s.GetAvailableSlotsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
            It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("S1"))]);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        drawRepo.Verify(r => r.SaveAsync(
            It.Is<DrawAttemptDto>(d =>
                d.Steps.Count == 7 &&
                d.Steps.Any(s => s.StepName == "PolicyResolved") &&
                d.Steps.Any(s => s.StepName == "RequestsLoaded") &&
                d.Steps.Any(s => s.StepName == "CapacityLoaded") &&
                d.Steps.Any(s => s.StepName == "MetricsLoaded") &&
                d.Steps.Any(s => s.StepName == "WeightedAllocationCompleted") &&
                d.Steps.Any(s => s.StepName == "DecisionsPersisted") &&
                d.Steps.Any(s => s.StepName == "EventsPublished")
            ),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CompletedDraw_AllStepsMarkedCompleted()
    {
        await handler.Handle(ValidCommand(), CancellationToken.None);

        drawRepo.Verify(r => r.SaveAsync(
            It.Is<DrawAttemptDto>(d =>
                d.Steps.All(s => s.Status == "Completed") &&
                d.Steps.All(s => s.StartedAt.HasValue) &&
                d.Steps.All(s => s.CompletedAt.HasValue)
            ),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CompletedDraw_CorrelationIdPresentInAllSteps()
    {
        await handler.Handle(ValidCommand(), CancellationToken.None);

        drawRepo.Verify(r => r.SaveAsync(
            It.Is<DrawAttemptDto>(d =>
                !string.IsNullOrEmpty(d.CorrelationId) &&
                d.Steps.All(s => s.CorrelationId == d.CorrelationId)
            ),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CompletedDraw_StepsHaveBusinessReadableSummaries()
    {
        var pending = PendingDto();
        bookingQueryRepo.Setup(r => r.GetPendingRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending]);

        slotService.Setup(s => s.GetAvailableSlotsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
            It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("S1"))]);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        drawRepo.Verify(r => r.SaveAsync(
            It.Is<DrawAttemptDto>(d =>
                d.Steps.All(s => !string.IsNullOrEmpty(s.Summary))
            ),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
