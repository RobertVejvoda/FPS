using FPS.Booking.Application.Exceptions;
using FPS.Booking.Domain.Exceptions;

namespace FPS.Booking.Application.Tests.Commands;

public sealed class ConfirmSlotUsageHandlerTests
{
    private readonly Mock<IBookingRepository> repository = new();
    private readonly Mock<IEventPublisher> eventPublisher = new();
    private readonly ConfirmSlotUsageHandler handler;

    public ConfirmSlotUsageHandlerTests()
    {
        handler = new ConfirmSlotUsageHandler(repository.Object, eventPublisher.Object);

        repository.Setup(r => r.UpdateBookingRequestUsageAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AllocatedRequest_ReturnsUsed()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AllocatedDto());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Used", result.Status);
        Assert.False(result.WasAlreadyConfirmed);
    }

    [Fact]
    public async Task Handle_AllocatedRequest_PersistsUsageConfirmation()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AllocatedDto());

        await handler.Handle(ValidCommand(), CancellationToken.None);

        repository.Verify(r => r.UpdateBookingRequestUsageAsync(
            It.IsAny<Guid>(), "EmployeeSelf", It.IsAny<DateTime>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AllocatedRequest_PublishesUsedEvent()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AllocatedDto());

        await handler.Handle(ValidCommand(), CancellationToken.None);

        eventPublisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.BookingRequestUsedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CustomConfirmedAt_UsesProvidedTimestamp()
    {
        var customTime = new DateTime(2026, 6, 2, 10, 30, 0, DateTimeKind.Utc);
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AllocatedDto());

        var result = await handler.Handle(ValidCommand(confirmedAt: customTime), CancellationToken.None);

        Assert.Equal(customTime, result.ConfirmedAt);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AlreadyUsed_ReturnsExistingStateWithoutDuplicatingEvents()
    {
        var usedAt = new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(UsedDto(usedAt));

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Used", result.Status);
        Assert.True(result.WasAlreadyConfirmed);
        eventPublisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.BookingRequestUsedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.UpdateBookingRequestUsageAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Error paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NotFound_ThrowsBookingNotFoundException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync((BookingRequestDto?)null);

        await Assert.ThrowsAsync<BookingNotFoundException>(() =>
            handler.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PendingRequest_ThrowsBookingException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DtoWithStatus("Pending"));

        await Assert.ThrowsAsync<BookingException>(() =>
            handler.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CancelledRequest_ThrowsBookingException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DtoWithStatus("Cancelled"));

        await Assert.ThrowsAsync<BookingException>(() =>
            handler.Handle(ValidCommand(), CancellationToken.None));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ConfirmSlotUsageCommand ValidCommand(DateTime? confirmedAt = null) => new(
        RequestId: Guid.NewGuid(),
        TenantId: "tenant-1",
        RequestorId: Guid.NewGuid().ToString(),
        ConfirmationSource: "EmployeeSelf",
        ConfirmedAt: confirmedAt,
        SourceEventId: null);

    private static BookingRequestDto AllocatedDto() => DtoWithStatus("Allocated");

    private static BookingRequestDto UsedDto(DateTime usedAt)
    {
        var dto = DtoWithStatus("Used");
        dto.UsageConfirmedAt = usedAt;
        dto.ConfirmationSource = "EmployeeSelf";
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
