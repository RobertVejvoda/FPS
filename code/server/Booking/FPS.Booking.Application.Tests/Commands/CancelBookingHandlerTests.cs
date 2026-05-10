using FPS.Booking.Application.Exceptions;
using FPS.Booking.Domain.Exceptions;

namespace FPS.Booking.Application.Tests.Commands;

public sealed class CancelBookingHandlerTests
{
    private readonly Mock<IBookingRepository> repository = new();
    private readonly Mock<IEventPublisher> eventPublisher = new();
    private readonly CancelBookingHandler handler;

    public CancelBookingHandlerTests()
    {
        handler = new CancelBookingHandler(repository.Object, eventPublisher.Object);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PendingRequest_ReturnsCancelled()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(PendingDto());
        repository.Setup(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("Cancelled", result.Status);
    }

    [Fact]
    public async Task Handle_PendingRequest_PersistsCancelledStatus()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(PendingDto());
        repository.Setup(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        repository.Verify(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), "Cancelled", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PendingRequest_PublishesCancelledEvent()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(PendingDto());
        repository.Setup(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        eventPublisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.BookingRequestCancelledEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NotFound_ThrowsBookingNotFoundException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync((BookingRequestDto?)null);

        await Assert.ThrowsAsync<BookingNotFoundException>(() =>
            handler.Handle(ValidCommand(), CancellationToken.None));
    }

    // ── Terminal state rejection ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_AlreadyCancelled_ThrowsBookingException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DtoWithStatus("Cancelled"));

        await Assert.ThrowsAsync<BookingException>(() =>
            handler.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RejectedRequest_ThrowsBookingException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(DtoWithStatus("Rejected"));

        await Assert.ThrowsAsync<BookingException>(() =>
            handler.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoPenaltyCreated_OnlyStatusUpdateIsCalled()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(PendingDto());
        repository.Setup(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        // Exactly one read and one status write — no allocation update, no penalty write
        repository.Verify(r => r.GetBookingRequestAsync(It.IsAny<Guid>()), Times.Once);
        repository.Verify(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), "Cancelled", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.UpdateAllocationStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CancelBookingCommand ValidCommand() => new(
        RequestId: Guid.NewGuid(),
        TenantId: "tenant-1",
        RequestorId: Guid.NewGuid().ToString(),
        Reason: "Changed plans");

    private static BookingRequestDto PendingDto() => DtoWithStatus("Pending");

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
