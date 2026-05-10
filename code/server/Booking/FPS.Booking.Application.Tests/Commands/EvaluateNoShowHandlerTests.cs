namespace FPS.Booking.Application.Tests.Commands;

public sealed class EvaluateNoShowHandlerTests
{
    private readonly Mock<IBookingRepository> repository = new();
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly Mock<IPenaltyRepository> penaltyRepository = new();
    private readonly Mock<ITenantPolicyService> policyService = new();
    private readonly Mock<IEventPublisher> eventPublisher = new();
    private readonly EvaluateNoShowHandler handler;

    private static readonly TenantPolicy EnabledPolicy = new(
        DailyRequestCap: 500, DrawCutOffTime: new TimeOnly(18, 0), TimeZoneId: "UTC",
        SameDayBookingEnabled: true, AllocationLookbackDays: 10,
        LateCancellationPenalty: 1, NoShowPenalty: 2,
        UsageConfirmationEnabled: true, UsageConfirmationWindowMinutes: 60,
        NoShowDetectionEnabled: true);

    public EvaluateNoShowHandlerTests()
    {
        handler = new EvaluateNoShowHandler(
            repository.Object, queryRepository.Object, penaltyRepository.Object,
            policyService.Object, eventPublisher.Object);

        policyService.Setup(s => s.GetEffectivePolicyAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnabledPolicy);

        queryRepository.Setup(r => r.GetAllocatedRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookingRequestDto>());

        penaltyRepository.Setup(r => r.ExistsAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        penaltyRepository.Setup(r => r.SaveAsync(
            It.IsAny<PenaltyDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        repository.Setup(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Policy guards ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UsageConfirmationDisabled_ReturnsSkipped()
    {
        policyService.Setup(s => s.GetEffectivePolicyAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnabledPolicy with { UsageConfirmationEnabled = false });

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(0, result.MarkedCount);
        Assert.NotNull(result.SkippedReason);
    }

    [Fact]
    public async Task Handle_NoShowDetectionDisabled_ReturnsSkipped()
    {
        policyService.Setup(s => s.GetEffectivePolicyAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnabledPolicy with { NoShowDetectionEnabled = false });

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(0, result.MarkedCount);
        Assert.NotNull(result.SkippedReason);
    }

    [Fact]
    public async Task Handle_NeitherEnabled_NeverUpdatesStatus()
    {
        policyService.Setup(s => s.GetEffectivePolicyAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnabledPolicy with { UsageConfirmationEnabled = false, NoShowDetectionEnabled = false });

        await handler.Handle(ValidCommand(), CancellationToken.None);

        repository.Verify(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_EligibleAllocatedRequest_MarksNoShow()
    {
        queryRepository.Setup(r => r.GetAllocatedRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AllocatedDto()]);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(1, result.MarkedCount);
        repository.Verify(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), "NoShow", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EligibleRequest_CreatesNoShowPenalty()
    {
        queryRepository.Setup(r => r.GetAllocatedRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AllocatedDto()]);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        penaltyRepository.Verify(r => r.SaveAsync(
            It.Is<PenaltyDto>(p => p.Type == "NoShow" && p.Score == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyConfirmed_SkippedNotMarked()
    {
        var dto = AllocatedDto();
        dto.ConfirmationSource = "EmployeeSelf";
        queryRepository.Setup(r => r.GetAllocatedRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(0, result.MarkedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task Handle_PenaltyIdempotent_NotCreatedTwice()
    {
        penaltyRepository.Setup(r => r.ExistsAsync(
            It.IsAny<Guid>(), "NoShow", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        queryRepository.Setup(r => r.GetAllocatedRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AllocatedDto()]);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        penaltyRepository.Verify(r => r.SaveAsync(
            It.IsAny<PenaltyDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EvaluateNoShowCommand ValidCommand() => new(
        TenantId: "tenant-1",
        LocationId: "loc-1",
        Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
        TimeSlotStart: DateTime.UtcNow.AddDays(-1).Date.AddHours(9),
        TimeSlotEnd: DateTime.UtcNow.AddDays(-1).Date.AddHours(17),
        Reason: "Scheduled evaluation");

    private static BookingRequestDto AllocatedDto() => new()
    {
        RequestId = Guid.NewGuid(),
        RequestedBy = Guid.NewGuid().ToString(),
        PlannedArrivalTime = DateTime.UtcNow.AddDays(-1).Date.AddHours(9),
        PlannedDepartureTime = DateTime.UtcNow.AddDays(-1).Date.AddHours(17),
        RequestedAt = DateTime.UtcNow.AddDays(-2),
        Status = "Allocated"
    };
}
