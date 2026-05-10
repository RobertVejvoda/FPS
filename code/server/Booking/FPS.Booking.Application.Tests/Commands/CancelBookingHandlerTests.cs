using FPS.Booking.Application.Exceptions;
using FPS.Booking.Domain.Exceptions;
using FPS.Booking.Domain.Services;

namespace FPS.Booking.Application.Tests.Commands;

public sealed class CancelBookingHandlerTests
{
    private readonly Mock<IBookingRepository> repository = new();
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly Mock<IPenaltyRepository> penaltyRepository = new();
    private readonly Mock<IDrawRepository> drawRepository = new();
    private readonly Mock<ITenantPolicyService> policyService = new();
    private readonly Mock<IEventPublisher> eventPublisher = new();
    private readonly CancelBookingHandler handler;

    private static readonly TenantPolicy DefaultPolicy = new(500, new TimeOnly(18, 0), "UTC", true, 10, 1, 2);

    public CancelBookingHandlerTests()
    {
        handler = new CancelBookingHandler(
            repository.Object, queryRepository.Object, penaltyRepository.Object,
            drawRepository.Object, policyService.Object, eventPublisher.Object, new DrawService());

        policyService.Setup(s => s.GetEffectivePolicyAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy);

        penaltyRepository.Setup(r => r.ExistsAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        penaltyRepository.Setup(r => r.SaveAsync(
            It.IsAny<PenaltyDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        drawRepository.Setup(r => r.GetByKeyAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        queryRepository.Setup(r => r.GetPendingRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookingRequestDto>());

        repository.Setup(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── B003 path: Pending cancellation (unchanged) ───────────────────────────

    [Fact]
    public async Task Handle_PendingRequest_ReturnsCancelled()
    {
        SetupRequest(PendingDto());
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);
        Assert.Equal("Cancelled", result.Status);
    }

    [Fact]
    public async Task Handle_PendingRequest_NoPenaltyCreated()
    {
        SetupRequest(PendingDto());
        await handler.Handle(ValidCommand(), CancellationToken.None);
        penaltyRepository.Verify(r => r.SaveAsync(It.IsAny<PenaltyDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PendingRequest_NoReallocationAttempted()
    {
        SetupRequest(PendingDto());
        await handler.Handle(ValidCommand(), CancellationToken.None);
        queryRepository.Verify(r => r.GetPendingRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── B005 path: Allocated cancellation ────────────────────────────────────

    [Fact]
    public async Task Handle_AllocatedRequest_ReturnsCancelled()
    {
        SetupRequest(AllocatedDto());
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);
        Assert.Equal("Cancelled", result.Status);
    }

    [Fact]
    public async Task Handle_AllocatedRequest_CreatesLateCancellationPenalty()
    {
        SetupRequest(AllocatedDto());
        await handler.Handle(ValidCommand(), CancellationToken.None);
        penaltyRepository.Verify(r => r.SaveAsync(
            It.Is<PenaltyDto>(p => p.Type == "LateCancellation" && p.Score == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AllocatedRequest_PenaltyIdempotent_NotCreatedTwice()
    {
        SetupRequest(AllocatedDto());
        penaltyRepository.Setup(r => r.ExistsAsync(
            It.IsAny<Guid>(), "LateCancellation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        penaltyRepository.Verify(r => r.SaveAsync(
            It.IsAny<PenaltyDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AllocatedRequest_PublishesPenaltyEvent()
    {
        SetupRequest(AllocatedDto());
        await handler.Handle(ValidCommand(), CancellationToken.None);
        eventPublisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.PenaltyAppliedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── B005 reallocation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AllocatedRequest_WhenPendingCandidateExists_Reallocates()
    {
        var dto = AllocatedDto();
        SetupRequest(dto);
        queryRepository.Setup(r => r.GetPendingRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PendingDto()]);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        repository.Verify(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), "Allocated", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AllocatedRequest_WhenNoCandidates_StillCancels()
    {
        SetupRequest(AllocatedDto());
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);
        Assert.Equal("Cancelled", result.Status);
    }

    [Fact]
    public async Task Handle_AllocatedRequest_Reallocated_PublishesReallocatedEvent()
    {
        SetupRequest(AllocatedDto());
        queryRepository.Setup(r => r.GetPendingRequestsForDrawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PendingDto()]);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        eventPublisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.BookingRequestReallocatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Error paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NotFound_ThrowsBookingNotFoundException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync((BookingRequestDto?)null);
        await Assert.ThrowsAsync<BookingNotFoundException>(() => handler.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyCancelled_ThrowsBookingException()
    {
        SetupRequest(DtoWithStatus("Cancelled"));
        await Assert.ThrowsAsync<BookingException>(() => handler.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Rejected_ThrowsBookingException()
    {
        SetupRequest(DtoWithStatus("Rejected"));
        await Assert.ThrowsAsync<BookingException>(() => handler.Handle(ValidCommand(), CancellationToken.None));
    }

    // ── Domain Penalty entity ─────────────────────────────────────────────────

    [Fact]
    public void Penalty_IsActiveOn_WithinWindow_ReturnsTrue()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var penalty = FPS.Booking.Domain.Entities.Penalty.Create(
            FPS.Booking.Domain.ValueObjects.BookingRequestId.New(),
            FPS.Booking.Domain.ValueObjects.UserId.New(),
            FPS.Booking.Domain.ValueObjects.PenaltyType.LateCancellation,
            score: 1, effectiveDate: today, expiryDays: 10, sourceEventId: "evt-1");

        Assert.True(penalty.IsActiveOn(today));
        Assert.True(penalty.IsActiveOn(today.AddDays(5)));
        Assert.False(penalty.IsActiveOn(today.AddDays(11)));
    }

    [Fact]
    public void Penalty_Create_InvalidScore_Throws()
    {
        Assert.Throws<FPS.Booking.Domain.Exceptions.BookingException>(() =>
            FPS.Booking.Domain.Entities.Penalty.Create(
                FPS.Booking.Domain.ValueObjects.BookingRequestId.New(),
                FPS.Booking.Domain.ValueObjects.UserId.New(),
                FPS.Booking.Domain.ValueObjects.PenaltyType.LateCancellation,
                score: 0, effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow),
                expiryDays: 10, sourceEventId: "evt-1"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupRequest(BookingRequestDto dto)
        => repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>())).ReturnsAsync(dto);

    private static CancelBookingCommand ValidCommand() => new(
        RequestId: Guid.NewGuid(),
        TenantId: "tenant-1",
        RequestorId: Guid.NewGuid().ToString(),
        Reason: "Changed plans");

    private static BookingRequestDto PendingDto() => DtoWithStatus("Pending");
    private static BookingRequestDto AllocatedDto()
    {
        var dto = DtoWithStatus("Allocated");
        dto.AllocatedSlotId = Guid.NewGuid();
        dto.LocationId = "loc-1";
        return dto;
    }

    private static BookingRequestDto DtoWithStatus(string status) => new()
    {
        RequestId = Guid.NewGuid(),
        RequestedBy = Guid.NewGuid().ToString(),
        PlannedArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9),
        PlannedDepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(17),
        RequestedAt = DateTime.UtcNow,
        Status = status
    };
}
