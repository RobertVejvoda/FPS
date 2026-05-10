using FPS.Notification.Application;
using FPS.Notification.Domain;
using Moq;

namespace FPS.Notification.Tests;

public sealed class BookingEventNotificationHandlerTests
{
    private readonly Mock<INotificationRepository> repository = new();
    private readonly BookingEventNotificationHandler handler;

    public BookingEventNotificationHandlerTests()
    {
        handler = new BookingEventNotificationHandler(repository.Object);
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_ValidEvent_SavesNotificationRecord()
    {
        var envelope = BuildEnvelope("booking.requestSubmitted", "user-1");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "user-1" &&
                n.TenantId == "tenant-1" &&
                n.NotificationType == "booking.requestSubmitted" &&
                n.Channel == NotificationChannel.InApp &&
                n.SourceEventId == "event-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEvent_DoesNotSaveAgain()
    {
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AllocationEvent_SetsConfirmUsageNextAction()
    {
        await handler.HandleAsync(BuildEnvelope("booking.slotAllocated", "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.NextAction == "confirmUsage"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CancellationWithAdditionalRecipient_NotifiesBothRecipients()
    {
        var envelope = BuildEnvelope("booking.requestCancelled", "user-1", additionalRecipientId: "user-2");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-1"),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EventWithReasonText_IncludesReasonInMessage()
    {
        var envelope = BuildEnvelope("booking.requestRejected", "user-1", reasonText: "No matching slot available");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.MessageText.Contains("No matching slot available")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EventWithNoRequestorId_SavesNothing()
    {
        var envelope = new BookingEventEnvelope(
            EventId: "event-1", EventType: "booking.drawCompleted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-1", CorrelationId: "corr-1",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(null, null, null, null, null, null, null, null, null, null));

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void DeduplicationKey_IsStable()
    {
        var key1 = BookingEventNotificationHandler.DeduplicationKey("evt-1", "user-1", "booking.slotAllocated");
        var key2 = BookingEventNotificationHandler.DeduplicationKey("evt-1", "user-1", "booking.slotAllocated");

        Assert.Equal(key1, key2);
    }

    private static BookingEventEnvelope BuildEnvelope(
        string eventType, string requestorId,
        string? additionalRecipientId = null,
        string? reasonText = null) => new(
        EventId: "event-1",
        EventType: eventType,
        EventVersion: 1,
        OccurredAt: DateTime.UtcNow,
        TenantId: "tenant-1",
        CorrelationId: "corr-1",
        CausationId: null,
        ActorType: "employee",
        ActorId: requestorId,
        Source: "booking",
        Payload: new BookingEventPayload(
            BookingRequestId: "req-1",
            RequestorId: requestorId,
            LocationId: "loc-1",
            Date: "2026-05-12",
            TimeSlot: "09:00-17:00",
            PreviousStatus: null,
            NewStatus: null,
            ReasonCode: null,
            ReasonText: reasonText,
            AdditionalRecipientId: additionalRecipientId));
}
