using FPS.Notification.Application;
using FPS.Notification.Domain;
using Moq;

namespace FPS.Notification.Tests;

public sealed class EmailNotificationHandlerTests
{
    private readonly Mock<INotificationRepository> repository = new();
    private readonly Mock<INotificationBroadcaster> broadcaster = new();
    private readonly Mock<IEmailNotificationSender> emailSender = new();
    private readonly BookingEventNotificationHandler handler;

    public EmailNotificationHandlerTests()
    {
        handler = new BookingEventNotificationHandler(repository.Object, broadcaster.Object, emailSender.Object);
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        emailSender.Setup(e => e.SendAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailSendResult.Ok());
    }

    [Fact]
    public async Task Handle_ValidEvent_SavesEmailRecord()
    {
        var envelope = BuildEnvelope("booking.requestSubmitted", "user-1");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "user-1" &&
                n.Channel == NotificationChannel.Email &&
                n.NotificationType == "booking.requestSubmitted" &&
                n.SourceEventId == "event-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidEvent_SendsEmailAndMarksSent()
    {
        await handler.HandleAsync(BuildEnvelope("booking.slotAllocated", "user-1"));

        emailSender.Verify(e => e.SendAsync(
            It.Is<NotificationRecord>(n => n.Channel == NotificationChannel.Email && n.RecipientId == "user-1"),
            It.IsAny<CancellationToken>()), Times.Once);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.Channel == NotificationChannel.Email &&
                n.DeliveryStatus == NotificationDeliveryStatus.Sent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailSenderFailure_MarksEmailRecordFailed_InAppUnaffected()
    {
        emailSender.Setup(e => e.SendAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailSendResult.Fail("SMTP timeout"));

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.Channel == NotificationChannel.Email &&
                n.DeliveryStatus == NotificationDeliveryStatus.Failed &&
                n.FailureReason == "SMTP timeout"),
            It.IsAny<CancellationToken>()), Times.Once);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.Channel == NotificationChannel.InApp &&
                n.DeliveryStatus == NotificationDeliveryStatus.Stored),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEvent_DoesNotResendEmail()
    {
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        emailSender.Verify(e => e.SendAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TwoRecipients_SendsEmailToEachIndependently()
    {
        var envelope = BuildEnvelope("booking.requestCancelled", "user-1",
            affectedRecipientIds: ["user-2"]);

        await handler.HandleAsync(envelope);

        emailSender.Verify(e => e.SendAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-1" && n.Channel == NotificationChannel.Email),
            It.IsAny<CancellationToken>()), Times.Once);
        emailSender.Verify(e => e.SendAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-2" && n.Channel == NotificationChannel.Email),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailAndInAppUseDistinctDeduplicationKeys()
    {
        var inAppKey = BookingEventNotificationHandler.DeduplicationKey("evt-1", "user-1", "booking.slotAllocated", NotificationChannel.InApp);
        var emailKey = BookingEventNotificationHandler.DeduplicationKey("evt-1", "user-1", "booking.slotAllocated", NotificationChannel.Email);

        Assert.NotEqual(inAppKey, emailKey);
    }

    private static BookingEventEnvelope BuildEnvelope(
        string eventType, string requestorId,
        IReadOnlyList<string>? affectedRecipientIds = null) => new(
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
            Date: "2026-05-14",
            TimeSlot: "09:00-17:00",
            PreviousStatus: null,
            NewStatus: null,
            ReasonCode: null,
            ReasonText: null,
            AffectedRecipientIds: affectedRecipientIds));
}
