using FPS.Booking.Application.Exceptions;
using FPS.Booking.Domain.Exceptions;

namespace FPS.Booking.Application.Tests.Commands;

public sealed class ApplyManualCorrectionHandlerTests
{
    private readonly Mock<IBookingRepository> repository = new();
    private readonly Mock<ICorrectionAuditRepository> auditRepository = new();
    private readonly Mock<IEventPublisher> eventPublisher = new();
    private readonly ApplyManualCorrectionHandler handler;

    public ApplyManualCorrectionHandlerTests()
    {
        handler = new ApplyManualCorrectionHandler(
            repository.Object, auditRepository.Object, eventPublisher.Object);

        repository.Setup(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        auditRepository.Setup(r => r.SaveAsync(
            It.IsAny<CorrectionAuditDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Reason validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_EmptyReason_ThrowsBookingException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(PendingDto());

        await Assert.ThrowsAsync<BookingException>(() =>
            handler.Handle(ValidCommand(reason: ""), CancellationToken.None));
    }

    // ── Optimistic concurrency ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OldValueMismatch_ThrowsCorrectionConflictException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(PendingDto()); // current = Pending

        await Assert.ThrowsAsync<CorrectionConflictException>(() =>
            handler.Handle(ValidCommand(correctionType: "status", oldValue: "Allocated", newValue: "Pending"),
                CancellationToken.None));
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RequestNotFound_ThrowsBookingNotFoundException()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync((BookingRequestDto?)null);

        await Assert.ThrowsAsync<BookingNotFoundException>(() =>
            handler.Handle(ValidCommand(), CancellationToken.None));
    }

    // ── Status correction ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_StatusCorrection_UpdatesStatus()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(PendingDto());

        var result = await handler.Handle(
            ValidCommand(correctionType: "status", oldValue: "Pending", newValue: "Allocated"),
            CancellationToken.None);

        Assert.Equal("status", result.CorrectionType);
        Assert.Equal("Allocated", result.NewValue);
        repository.Verify(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<Guid>(), "Allocated", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Audit record ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCorrection_SavesAuditRecord()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(PendingDto());

        await handler.Handle(
            ValidCommand(correctionType: "status", oldValue: "Pending", newValue: "Allocated"),
            CancellationToken.None);

        auditRepository.Verify(r => r.SaveAsync(
            It.Is<CorrectionAuditDto>(a =>
                a.CorrectionType == "status" &&
                a.OldValue == "Pending" &&
                a.NewValue == "Allocated" &&
                !string.IsNullOrEmpty(a.Reason)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCorrection_PublishesManualCorrectionEvent()
    {
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(PendingDto());

        await handler.Handle(
            ValidCommand(correctionType: "status", oldValue: "Pending", newValue: "Allocated"),
            CancellationToken.None);

        eventPublisher.Verify(p => p.PublishAsync(
            It.IsAny<FPS.Booking.Domain.Events.ManualCorrectionAppliedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Reason correction ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReasonCorrection_MatchesOldValue()
    {
        var dto = PendingDto();
        dto.RejectionReason = "Old reason";
        repository.Setup(r => r.GetBookingRequestAsync(It.IsAny<Guid>()))
            .ReturnsAsync(dto);
        repository.Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>()))
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(
            ValidCommand(correctionType: "reason", oldValue: "Old reason", newValue: "Corrected reason"),
            CancellationToken.None);

        Assert.Equal("Corrected reason", result.NewValue);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ApplyManualCorrectionCommand ValidCommand(
        string correctionType = "status",
        string oldValue = "Pending",
        string newValue = "Allocated",
        string reason = "HR manual correction for edge case") => new(
            RequestId: Guid.NewGuid(),
            TenantId: "tenant-1",
            Actor: "hr-user-1",
            CorrectionType: correctionType,
            OldValue: oldValue,
            NewValue: newValue,
            Reason: reason,
            EffectiveAt: null);

    private static BookingRequestDto PendingDto() => new()
    {
        RequestId = Guid.NewGuid(),
        RequestedBy = Guid.NewGuid().ToString(),
        PlannedArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9),
        PlannedDepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(17),
        RequestedAt = DateTime.UtcNow,
        Status = "Pending"
    };
}
