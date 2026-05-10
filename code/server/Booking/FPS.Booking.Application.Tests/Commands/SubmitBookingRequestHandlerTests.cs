namespace FPS.Booking.Application.Tests.Commands;

public class SubmitBookingRequestHandlerTests
{
    private readonly Mock<IBookingRepository> _repository = new();
    private readonly Mock<ITenantPolicyService> _policyService = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly SubmitBookingRequestHandler _handler;

    private static readonly TenantPolicy DefaultPolicy = new(
        DailyRequestCap: 500,
        DrawCutOffTime: new TimeOnly(18, 0),
        TimeZoneId: "UTC",
        SameDayBookingEnabled: true);

    public SubmitBookingRequestHandlerTests()
    {
        _handler = new SubmitBookingRequestHandler(
            _repository.Object,
            _policyService.Object,
            _publisher.Object);

        _policyService
            .Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy);

        _repository
            .Setup(r => r.CountRequestsForDateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _repository
            .Setup(r => r.HasOverlappingRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>()))
            .Returns(Task.CompletedTask);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidFutureRequest_ReturnsPendingStatus()
    {
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Pending", result.Status);
        Assert.Null(result.RejectionCode);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsBookingRequest()
    {
        await _handler.Handle(ValidCommand(), CancellationToken.None);

        _repository.Verify(r => r.CreateBookingRequestAsync(
            It.Is<BookingRequestDto>(dto => dto.Status == "Pending")), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsNonEmptyRequestId()
    {
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.RequestId);
    }

    // ── Cut-off rejection ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CutOffPassed_ReturnsRejected()
    {
        var latePolicy = DefaultPolicy with { DrawCutOffTime = new TimeOnly(0, 0) }; // midnight = always past
        _policyService
            .Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(latePolicy);

        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("CutOffPassed", result.RejectionCode);
    }

    // ── Daily cap rejection ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DailyCapExceeded_ReturnsRejected()
    {
        _repository
            .Setup(r => r.CountRequestsForDateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500);

        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("DailyCapExceeded", result.RejectionCode);
    }

    // ── Duplicate rejection ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateRequest_ReturnsRejected()
    {
        _repository
            .Setup(r => r.HasOverlappingRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("DuplicateRequest", result.RejectionCode);
    }

    // ── Rejected request is still persisted ──────────────────────────────────

    [Fact]
    public async Task Handle_RejectedRequest_StillPersisted()
    {
        _repository
            .Setup(r => r.HasOverlappingRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _handler.Handle(ValidCommand(), CancellationToken.None);

        _repository.Verify(r => r.CreateBookingRequestAsync(
            It.Is<BookingRequestDto>(dto => dto.Status == "Rejected")), Times.Once);
    }

    // ── Domain events are published ───────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidRequest_PublishesDomainEvents()
    {
        await _handler.Handle(ValidCommand(), CancellationToken.None);

        _publisher.Verify(p => p.PublishAsync(It.IsAny<FPS.Booking.Domain.Events.BookingRequestSubmittedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

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
