namespace FPS.Booking.Application.Tests.Commands;

public sealed class SubmitBookingRequestHandlerTests
{
    private readonly Mock<IBookingRepository> repository = new();
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly Mock<ITenantPolicyService> policyService = new();
    private readonly Mock<IEventPublisher> publisher = new();
    private readonly SubmitBookingRequestHandler handler;

    private static readonly TenantPolicy DefaultPolicy = new(
        DailyRequestCap: 500,
        DrawCutOffTime: new TimeOnly(18, 0),
        TimeZoneId: "UTC",
        SameDayBookingEnabled: true,
        AllocationLookbackDays: 10);

    public SubmitBookingRequestHandlerTests()
    {
        handler = new SubmitBookingRequestHandler(
            repository.Object, queryRepository.Object, policyService.Object, publisher.Object);

        policyService
            .Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy);

        repository
            .Setup(r => r.CountRequestsForDateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        repository
            .Setup(r => r.HasOverlappingRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        repository
            .Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>()))
            .Returns(Task.CompletedTask);

        queryRepository
            .Setup(r => r.AddToUserIndexAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidFutureRequest_ReturnsPendingStatus()
    {
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Pending", result.Status);
        Assert.Null(result.RejectionCode);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsBookingRequest()
    {
        await handler.Handle(ValidCommand(), CancellationToken.None);

        repository.Verify(r => r.CreateBookingRequestAsync(
            It.Is<BookingRequestDto>(dto => dto.Status == "Pending")), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsToUserIndex()
    {
        var cmd = ValidCommand();
        await handler.Handle(cmd, CancellationToken.None);

        queryRepository.Verify(r => r.AddToUserIndexAsync(
            cmd.TenantId, cmd.RequestorId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectedRequest_StillAddsToUserIndex()
    {
        repository
            .Setup(r => r.HasOverlappingRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        queryRepository.Verify(r => r.AddToUserIndexAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsNonEmptyRequestId()
    {
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, result.RequestId);
    }

    // ── Rejection paths ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CutOffPassed_ReturnsRejected()
    {
        var latePolicy = DefaultPolicy with { DrawCutOffTime = new TimeOnly(0, 0) };
        policyService
            .Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(latePolicy);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("CutOffPassed", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_DailyCapExceeded_ReturnsRejected()
    {
        repository
            .Setup(r => r.CountRequestsForDateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("DailyCapExceeded", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_DuplicateRequest_ReturnsRejected()
    {
        repository
            .Setup(r => r.HasOverlappingRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("DuplicateRequest", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesDomainEvents()
    {
        await handler.Handle(ValidCommand(), CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.BookingRequestSubmittedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static SubmitBookingRequestCommand ValidCommand() => new(
        TenantId: "tenant-1",
        RequestorId: Guid.NewGuid().ToString(),
        FacilityId: Guid.NewGuid().ToString(),
        LocationId: null,
        LicensePlate: "ABC-123",
        VehicleType: "Sedan",
        IsElectric: false,
        RequiresAccessibleSpot: false,
        IsCompanyCar: false,
        PlannedArrivalTime: DateTime.UtcNow.AddDays(1).Date.AddHours(9),
        PlannedDepartureTime: DateTime.UtcNow.AddDays(1).Date.AddHours(17));
}
