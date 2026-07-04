using FPS.Notification.Application;
using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Notification.Tests;

public sealed class BookingEventNotificationHandlerTests
{
    private readonly Mock<INotificationRepository> repository = new();
    private readonly Mock<INotificationBroadcaster> broadcaster = new();
    private readonly Mock<IEmailNotificationSender> emailSender = new();
    private readonly InMemoryHrRosterStore roster = new();
    private readonly BookingEventNotificationHandler handler;

    public BookingEventNotificationHandlerTests()
    {
        handler = new BookingEventNotificationHandler(repository.Object, broadcaster.Object, emailSender.Object,
            new EmailNotificationComposer(),
            new InMemoryNotificationPreferencesRepository(),
            new RosterBackedAudienceResolver(roster),
            NullLogger<BookingEventNotificationHandler>.Instance);
        emailSender.Setup(e => e.SendAsync(It.IsAny<NotificationRecord>(), It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailSendResult.Ok());
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
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
                n.SourceEventId == "event-1" &&
                n.DeliveryStatus == NotificationDeliveryStatus.Stored),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEvent_DoesNotSaveAgain()
    {
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AllocationEvent_SetsConfirmUsageNextAction()
    {
        await handler.HandleAsync(BuildEnvelope("booking.slotAllocated", "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.NextAction == "confirmUsage" && n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AffectedRecipientIds_NotifiesAllRecipients()
    {
        var envelope = BuildEnvelope("booking.requestCancelled", "user-1",
            affectedRecipientIds: ["user-2", "user-3"]);

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-1" && n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-2" && n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-3" && n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateInAffectedRecipientIds_DeduplicatesRecipients()
    {
        // requestorId also appears in affectedRecipientIds — should only notify once
        var envelope = BuildEnvelope("booking.slotAllocated", "user-1",
            affectedRecipientIds: ["user-1", "user-2"]);

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-1" && n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-2" && n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EventWithReasonText_IncludesReasonInMessage()
    {
        var envelope = BuildEnvelope("booking.requestRejected", "user-1", reasonText: "No matching slot available");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.MessageText.Contains("No matching slot available") && n.Channel == NotificationChannel.InApp),
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
    public async Task Handle_ValidEvent_BroadcastsAfterSave()
    {
        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        broadcaster.Verify(b => b.BroadcastAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEvent_DoesNotBroadcast()
    {
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        broadcaster.Verify(b => b.BroadcastAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void DeduplicationKey_IsStable()
    {
        var key1 = BookingEventNotificationHandler.DeduplicationKey("evt-1", "user-1", "booking.slotAllocated");
        var key2 = BookingEventNotificationHandler.DeduplicationKey("evt-1", "user-1", "booking.slotAllocated");

        Assert.Equal(key1, key2);
    }

    // ── Message content (NOTIF002) ────────────────────────────────────────────

    [Theory]
    [InlineData("booking.requestSubmitted")]
    [InlineData("booking.slotAllocated")]
    [InlineData("booking.requestCancelled")]
    [InlineData("booking.noShowRecorded")]
    [InlineData("booking.usageConfirmed")]
    [InlineData("booking.requestExpired")]
    public async Task Handle_DateInPayload_MessageIncludesDate(string eventType)
    {
        await handler.HandleAsync(BuildEnvelope(eventType, "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.MessageText.Contains("12 May 2026") && n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectedWithKnownReasonCode_UsesEmployeeSafeText()
    {
        var envelope = BuildEnvelopeFull("booking.requestRejected", "user-1",
            reasonCode: "DrawNotSelected", reasonText: "Internal draw detail that should not appear");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.MessageText.Contains("not selected in the draw") &&
                !n.MessageText.Contains("Internal draw detail") &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectedWithNoCapacity_UsesEmployeeSafeText()
    {
        var envelope = BuildEnvelopeFull("booking.requestRejected", "user-1", reasonCode: "NoCapacityAvailable");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.MessageText.Contains("no available spots") &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectedWithUnknownReasonCode_FallsBackToReasonText()
    {
        var envelope = BuildEnvelopeFull("booking.requestRejected", "user-1",
            reasonCode: "SomeUnknownCode", reasonText: "Custom reason text");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.MessageText.Contains("Custom reason text") &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Reallocation_MessageDistinguishesFromNormalAllocation()
    {
        var envelope = BuildEnvelopeFull("booking.slotAllocated", "user-1", allocationSource: "reallocation");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.MessageText.Contains("reallocated") &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HrCancellation_MessageMentionsHr()
    {
        var envelope = BuildEnvelopeFull("booking.requestCancelled", "user-1", actorType: "hr_manager");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.MessageText.Contains("HR") &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmployeeCancellation_MessageDoesNotMentionHr()
    {
        var envelope = BuildEnvelopeFull("booking.requestCancelled", "user-1", actorType: "employee");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                !n.MessageText.Contains("HR") &&
                n.MessageText.Contains("cancelled") &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DrawCompletedWithCounts_MessageIncludesSummary()
    {
        // Simulate a draw event with a recipient present (future HR routing).
        var envelope = BuildEnvelopeFull("booking.drawCompleted", requestorId: "hr-user-1",
            allocatedCount: 8, rejectedCount: 4, waitlistedCount: 0);

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.MessageText.Contains("8") &&
                n.MessageText.Contains("12") &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DrawCompletedNoCounts_MessageIsGeneric()
    {
        // Simulate a draw event with a recipient present (future HR routing).
        var envelope = BuildEnvelopeFull("booking.drawCompleted", requestorId: "hr-user-1");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.MessageText.Contains("complete") &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── HR fan-out (NOTIF #478) ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_RequestSubmitted_FansOutToHrRecipients()
    {
        // New submission must reach HR so they can plan around the upcoming
        // draw; the employee keeps their own confirmation notification.
        roster.Set("tenant-1", ["hr-1", "hr-2"]);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "user-1" &&
                n.NotificationType == "booking.requestSubmitted" &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "hr-1" &&
                n.NotificationType == "booking.requestSubmitted.hr" &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "hr-2" &&
                n.NotificationType == "booking.requestSubmitted.hr" &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HrTargetedNotification_UsesAudienceLanguage()
    {
        // HR copy uses third-person business framing — never "your request".
        roster.Set("tenant-1", ["hr-1"]);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "hr-1" &&
                n.Channel == NotificationChannel.InApp &&
                n.MessageText.Contains("new parking request") &&
                !n.MessageText.Contains("Your")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoHrRoster_OnlyNotifiesRequestor()
    {
        // Empty roster: degrade gracefully — no extra notifications, no
        // exception. Important because tenants without registered HR users
        // must still see employee notifications.
        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "user-1" && n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.NotificationType.EndsWith(".hr")),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmployeeNotInHrRoster_DoesNotReceiveHrVariant()
    {
        // Role isolation: an employee not on the HR roster only gets the
        // requestor-facing notification, never the HR variant.
        roster.Set("tenant-1", ["hr-1"]);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "user-1" &&
                n.NotificationType == "booking.requestSubmitted.hr"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmployeeCancellation_NotifiesHr()
    {
        // HR must know when an employee cancels so they can manage capacity.
        roster.Set("tenant-1", ["hr-1"]);

        await handler.HandleAsync(BuildEnvelopeFull("booking.requestCancelled", "user-1", actorType: "employee"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "hr-1" &&
                n.Channel == NotificationChannel.InApp &&
                n.NotificationType == "booking.requestCancelled.hr" &&
                n.MessageText.Contains("employee has cancelled")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HrCancellation_DoesNotNotifyHrAudience()
    {
        // When HR cancels, they already know — don't echo back to the HR
        // roster. The affected employee still gets their notification.
        roster.Set("tenant-1", ["hr-1"]);

        await handler.HandleAsync(BuildEnvelopeFull("booking.requestCancelled", "user-1", actorType: "hr_manager"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.NotificationType.EndsWith(".hr")),
            It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "user-1" &&
                n.Channel == NotificationChannel.InApp &&
                n.MessageText.Contains("HR")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DrawCompleted_NotifiesHrAudience()
    {
        // Draw completion is a key business signal for HR oversight.
        roster.Set("tenant-1", ["hr-1"]);

        await handler.HandleAsync(BuildEnvelopeFull("booking.drawCompleted",
            requestorId: "user-1", allocatedCount: 5, rejectedCount: 2, waitlistedCount: 1, actorType: "system"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "hr-1" &&
                n.Channel == NotificationChannel.InApp &&
                n.NotificationType == "booking.drawCompleted.hr" &&
                n.MessageText.Contains("parking draw") &&
                n.MessageText.Contains("5") &&
                n.MessageText.Contains("8")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RequestorIsAlsoHr_GetsBothVariants()
    {
        // Edge case: a user with HR role who submits a personal request gets
        // both the requestor-facing and HR-facing notifications. They are
        // distinct deduplication keys so this isn't a duplicate.
        roster.Set("tenant-1", ["user-1"]);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "user-1" &&
                n.NotificationType == "booking.requestSubmitted" &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.RecipientId == "user-1" &&
                n.NotificationType == "booking.requestSubmitted.hr" &&
                n.Channel == NotificationChannel.InApp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HrFanOutEventOnOtherTenant_DoesNotLeakAcrossTenants()
    {
        // Roster lookup is keyed by tenantId — events for tenant-1 must not
        // pick up HR users registered under tenant-2.
        roster.Set("tenant-2", ["hr-other"]);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.RecipientId == "hr-other"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static BookingEventEnvelope BuildEnvelope(
        string eventType, string requestorId,
        IReadOnlyList<string>? affectedRecipientIds = null,
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
            AffectedRecipientIds: affectedRecipientIds));

    private static BookingEventEnvelope BuildEnvelopeFull(
        string eventType,
        string? requestorId = "user-1",
        string? reasonCode = null,
        string? reasonText = null,
        string? allocationSource = null,
        string? actorType = "employee",
        int? allocatedCount = null,
        int? rejectedCount = null,
        int? waitlistedCount = null) => new(
        EventId: "event-1",
        EventType: eventType,
        EventVersion: 1,
        OccurredAt: DateTime.UtcNow,
        TenantId: "tenant-1",
        CorrelationId: "corr-1",
        CausationId: null,
        ActorType: actorType ?? "employee",
        ActorId: requestorId,
        Source: "booking",
        Payload: new BookingEventPayload(
            BookingRequestId: requestorId is not null ? "req-1" : null,
            RequestorId: requestorId,
            LocationId: "loc-1",
            Date: "2026-05-12",
            TimeSlot: "09:00-17:00",
            PreviousStatus: null,
            NewStatus: null,
            ReasonCode: reasonCode,
            ReasonText: reasonText,
            AffectedRecipientIds: null,
            AllocationSource: allocationSource,
            AllocatedCount: allocatedCount,
            RejectedCount: rejectedCount,
            WaitlistedCount: waitlistedCount));
}
