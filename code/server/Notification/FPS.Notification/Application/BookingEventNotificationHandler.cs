using FPS.Notification.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FPS.Notification.Application;

public sealed class BookingEventNotificationHandler(
    INotificationRepository repository,
    INotificationBroadcaster broadcaster,
    IEmailNotificationSender emailSender,
    IEmailNotificationComposer emailComposer,
    IEmailRecipientResolver emailRecipientResolver,
    INotificationPreferencesRepository preferencesRepository,
    INotificationAudienceResolver audienceResolver,
    ILogger<BookingEventNotificationHandler> logger,
    IOptions<NotificationLocaleOptions>? localeOptions = null)
{
    // HR-variant notification types. The handler appends ".hr" to the
    // source event type when fanning out to HR users so the dedup key,
    // message switch and frontend filters can distinguish the two
    // recipients of the same source event without collisions.
    public const string HrSuffix = ".hr";

    // Event types where HR has a business interest in addition to the
    // requestor. For booking.requestCancelled we only fan out when the
    // actor is an employee — HR-initiated cancellations don't need to
    // notify HR back.
    private static readonly HashSet<string> HrFanoutEventTypes = new(StringComparer.Ordinal)
    {
        "booking.requestSubmitted",
        "booking.requestCancelled",
        "booking.drawCompleted",
    };

    // LOC001 (#744) — TODO: per-tenant/per-recipient locale resolution is a documented follow-up for a
    // later slice. Until then every recipient notified by this service instance gets the same
    // service-wide default locale (Notification:DefaultLocale), regardless of who they are.
    private readonly string locale = NotificationMessages.NormalizeLocale(localeOptions?.Value.DefaultLocale);

    public async Task HandleAsync(BookingEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Notification event received. TenantId={TenantId} EventType={EventType} SourceEventId={SourceEventId}",
            envelope.TenantId, envelope.EventType, envelope.EventId);

        var deliveries = await ResolveDeliveriesAsync(envelope, cancellationToken);

        var recipientCount = 0;
        foreach (var delivery in deliveries)
        {
            var notificationClass = NotificationClassifier.Classify(delivery.EffectiveType);
            var prefs = await preferencesRepository.GetOrDefaultAsync(envelope.TenantId, delivery.RecipientId, cancellationToken);
            if (!prefs.AllowsDelivery(notificationClass))
            {
                logger.LogDebug(
                    "Notification suppressed by user preference. TenantId={TenantId} NotificationType={NotificationType} Class={Class}",
                    envelope.TenantId, delivery.EffectiveType, notificationClass);
                continue;
            }

            await HandleInAppAsync(envelope, delivery, cancellationToken);
            await HandleEmailAsync(envelope, delivery, cancellationToken);
            recipientCount++;
        }

        logger.LogInformation(
            "Notification event processed. TenantId={TenantId} EventType={EventType} SourceEventId={SourceEventId} RecipientCount={RecipientCount}",
            envelope.TenantId, envelope.EventType, envelope.EventId, recipientCount);
    }

    private async Task HandleInAppAsync(BookingEventEnvelope envelope, DeliveryTarget delivery, CancellationToken cancellationToken)
    {
        var dedupKey = DeduplicationKey(envelope.EventId, delivery.RecipientId, delivery.EffectiveType, NotificationChannel.InApp);
        if (await repository.ExistsAsync(dedupKey, envelope.TenantId, cancellationToken))
            return;

        var record = CreateRecord(envelope, delivery, NotificationChannel.InApp, dedupKey, locale);
        await repository.SaveAsync(record, cancellationToken);
        // Best-effort — broadcaster failure must not affect persistence
        try { await broadcaster.BroadcastAsync(record, cancellationToken); } catch { }
    }

    private async Task HandleEmailAsync(BookingEventEnvelope envelope, DeliveryTarget delivery, CancellationToken cancellationToken)
    {
        var dedupKey = DeduplicationKey(envelope.EventId, delivery.RecipientId, delivery.EffectiveType, NotificationChannel.Email);
        if (await repository.ExistsAsync(dedupKey, envelope.TenantId, cancellationToken))
            return;

        var record = CreateRecord(envelope, delivery, NotificationChannel.Email, dedupKey, locale);

        // NOTIF #728 — resolve the recipient user ID to a verified email before any provider send.
        // Fail closed on an unresolved/unverified/malformed recipient: record a delivery-rejected
        // outcome and skip SendGrid. The in-app record is persisted independently in HandleInAppAsync,
        // so this never blocks in-app delivery. No recipient ID or address is logged.
        var recipient = await emailRecipientResolver.ResolveAsync(envelope.TenantId, delivery.RecipientId, cancellationToken);
        if (!recipient.Resolved)
        {
            record.MarkFailed(recipient.RejectionReason ?? "recipient_email_unavailable");
            logger.LogWarning(
                "Email delivery rejected: recipient email not resolved. TenantId={TenantId} NotificationType={NotificationType} SourceEventId={SourceEventId} Channel={Channel} FailureCategory={FailureCategory}",
                record.TenantId, record.NotificationType, record.SourceEventId, record.Channel, EmailFailureCategory.DeliveryRejected);
            await repository.SaveAsync(record, cancellationToken);
            return;
        }

        var composed = emailComposer.Compose(record, locale);
        EmailSendResult result;
        try { result = await emailSender.SendAsync(record, recipient.Email!, composed, cancellationToken); }
        catch { result = EmailSendResult.Fail("Email delivery unavailable", EmailFailureCategory.ProviderUnavailable); }

        if (result.Success)
        {
            record.MarkDelivered();
        }
        else
        {
            record.MarkFailed(result.FailureReason ?? "Unknown error");
            logger.LogWarning(
                "Email delivery failed. TenantId={TenantId} NotificationType={NotificationType} SourceEventId={SourceEventId} Channel={Channel} FailureCategory={FailureCategory}",
                record.TenantId, record.NotificationType, record.SourceEventId, record.Channel,
                result.FailureCategory ?? EmailFailureCategory.DeliveryRejected);
        }

        await repository.SaveAsync(record, cancellationToken);
    }

    private static NotificationRecord CreateRecord(
        BookingEventEnvelope envelope, DeliveryTarget delivery, string channel, string dedupKey, string locale) => new()
    {
        Id = Guid.NewGuid(),
        DeduplicationKey = dedupKey,
        TenantId = envelope.TenantId,
        RecipientId = delivery.RecipientId,
        NotificationType = delivery.EffectiveType,
        Channel = channel,
        MessageText = ResolveMessage(envelope, delivery.EffectiveType, locale),
        RelatedRequestId = envelope.Payload.BookingRequestId,
        RelatedDate = envelope.Payload.Date,
        RelatedTimeSlot = envelope.Payload.TimeSlot,
        LocationId = envelope.Payload.LocationId,
        NextAction = ResolveNextAction(delivery.EffectiveType),
        // #727 — carry safe outcome differentiators so the composer can pick variant-specific
        // templates (reallocation, allocated-reservation cancellation, penalty type).
        AllocationSource = envelope.Payload.AllocationSource,
        ReasonCode = envelope.Payload.ReasonCode,
        PreviousStatus = envelope.Payload.PreviousStatus,
        SourceEventId = envelope.EventId,
        CreatedAt = DateTime.UtcNow
    };

    private async Task<IReadOnlyList<DeliveryTarget>> ResolveDeliveriesAsync(
        BookingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var deliveries = new List<DeliveryTarget>();
        var seen = new HashSet<(string Recipient, string Type)>(EqualityComparer<(string, string)>.Default);

        void TryAdd(string? recipientId, string effectiveType)
        {
            if (string.IsNullOrEmpty(recipientId)) return;
            if (seen.Add((recipientId, effectiveType)))
                deliveries.Add(new DeliveryTarget(recipientId, effectiveType));
        }

        // Requestor-targeted notification keeps the source event type.
        TryAdd(envelope.Payload.RequestorId, envelope.EventType);

        if (envelope.Payload.AffectedRecipientIds is { Count: > 0 })
        {
            foreach (var id in envelope.Payload.AffectedRecipientIds)
                TryAdd(id, envelope.EventType);
        }

        // HR fan-out. Skipped when the actor is HR/admin so that, e.g., an
        // HR cancellation does not page the rest of the HR team about an
        // action they just performed.
        if (HrFanoutEventTypes.Contains(envelope.EventType) &&
            !IsHrActor(envelope.ActorType))
        {
            var hrRecipients = await audienceResolver.GetHrRecipientsAsync(envelope.TenantId, cancellationToken);
            var hrType = envelope.EventType + HrSuffix;
            foreach (var hrUser in hrRecipients)
                TryAdd(hrUser, hrType);
        }

        return deliveries;
    }

    private static bool IsHrActor(string actorType) =>
        actorType is "hr_manager" or "admin";

    private sealed record DeliveryTarget(string RecipientId, string EffectiveType);

    private static string ResolveMessage(BookingEventEnvelope envelope, string effectiveType, string locale)
    {
        var p = envelope.Payload;
        var ctx = BuildContext(p.Date, p.LocationId, p.TimeSlot, locale);

        return effectiveType switch
        {
            "booking.requestSubmitted" =>
                NotificationMessages.Resolve(locale, "booking.requestSubmitted.message", ctx),

            "booking.requestSubmitted" + HrSuffix =>
                NotificationMessages.Resolve(locale, "booking.requestSubmitted.hr.message", ctx),

            "booking.requestRejected" =>
                BuildRejectionMessage(p, ctx, locale),

            "booking.slotAllocated" =>
                p.AllocationSource == "reallocation"
                    ? NotificationMessages.Resolve(locale, "booking.slotAllocated.reallocation.message", ctx)
                    : NotificationMessages.Resolve(locale, "booking.slotAllocated.message", ctx),

            "booking.requestCancelled" =>
                BuildCancelledMessage(p, envelope.ActorType, locale),

            "booking.requestCancelled" + HrSuffix =>
                BuildHrCancellationMessage(p, ctx, locale),

            "booking.drawCompleted" =>
                BuildDrawCompletedMessage(p, ctx, hrAudience: false, locale),

            "booking.drawCompleted" + HrSuffix =>
                BuildDrawCompletedMessage(p, ctx, hrAudience: true, locale),

            "booking.noShowRecorded" =>
                NotificationMessages.Resolve(locale, "booking.noShowRecorded.message", ctx),

            "booking.penaltyApplied" =>
                BuildPenaltyMessage(p, locale),

            "booking.usageConfirmed" =>
                NotificationMessages.Resolve(locale, "booking.usageConfirmed.message", ctx),

            "booking.requestExpired" =>
                NotificationMessages.Resolve(locale, "booking.requestExpired.message", ctx),

            "booking.manualCorrectionApplied" =>
                string.IsNullOrEmpty(p.ReasonText)
                    ? NotificationMessages.Resolve(locale, "booking.manualCorrectionApplied.noReason.message")
                    : NotificationMessages.Resolve(locale, "booking.manualCorrectionApplied.reason.message", p.ReasonText),

            _ => NotificationMessages.Resolve(locale, "booking.unknown.message", effectiveType)
        };
    }

    private static string BuildContext(string? date, string? locationId, string? timeSlot, string locale)
    {
        if (string.IsNullOrEmpty(date)) return string.Empty;
        var datePart = NotificationMessages.FormatDate(date, locale);
        var location = string.IsNullOrEmpty(locationId) ? string.Empty : NotificationMessages.Resolve(locale, "context.location", locationId);
        var slot = string.IsNullOrEmpty(timeSlot) ? string.Empty : NotificationMessages.Resolve(locale, "context.slot", timeSlot);
        return NotificationMessages.Resolve(locale, "context.suffix", datePart, location, slot);
    }

    private static string BuildRejectionMessage(BookingEventPayload p, string ctx, string locale)
    {
        var reason = !string.IsNullOrEmpty(p.ReasonCode) && NotificationMessages.TryResolve(locale, $"rejection.{p.ReasonCode}", out var safe)
            ? safe
            : !string.IsNullOrEmpty(p.ReasonText) ? p.ReasonText : null;

        return reason is not null
            ? NotificationMessages.Resolve(locale, "booking.requestRejected.reason.message", ctx, reason)
            : NotificationMessages.Resolve(locale, "booking.requestRejected.noReason.message", ctx);
    }

    private static string BuildCancelledMessage(BookingEventPayload p, string actorType, string locale)
    {
        var ctx = BuildContext(p.Date, p.LocationId, p.TimeSlot, locale);
        var isHr = actorType is "hr_manager" or "admin";
        if (isHr)
        {
            return string.IsNullOrEmpty(p.ReasonText)
                ? NotificationMessages.Resolve(locale, "booking.requestCancelled.byHr.noReason.message", ctx)
                : NotificationMessages.Resolve(locale, "booking.requestCancelled.byHr.reason.message", ctx, p.ReasonText);
        }
        return NotificationMessages.Resolve(locale, "booking.requestCancelled.message", ctx);
    }

    private static string BuildDrawCompletedMessage(BookingEventPayload p, string ctx, bool hrAudience, string locale)
    {
        if (p.AllocatedCount.HasValue && p.RejectedCount.HasValue)
        {
            var total = p.AllocatedCount.Value + p.RejectedCount.Value + (p.WaitlistedCount ?? 0);
            return hrAudience
                ? NotificationMessages.Resolve(locale, "booking.drawCompleted.hr.withCounts.message", ctx, p.AllocatedCount, total)
                : NotificationMessages.Resolve(locale, "booking.drawCompleted.withCounts.message", ctx, p.AllocatedCount, total);
        }
        return hrAudience
            ? NotificationMessages.Resolve(locale, "booking.drawCompleted.hr.noCounts.message", ctx)
            : NotificationMessages.Resolve(locale, "booking.drawCompleted.noCounts.message", ctx);
    }

    private static string BuildHrCancellationMessage(BookingEventPayload p, string ctx, string locale)
    {
        return string.IsNullOrEmpty(p.ReasonText)
            ? NotificationMessages.Resolve(locale, "booking.requestCancelled.hr.noReason.message", ctx)
            : NotificationMessages.Resolve(locale, "booking.requestCancelled.hr.reason.message", ctx, p.ReasonText);
    }

    private static string BuildPenaltyMessage(BookingEventPayload p, string locale)
    {
        // ReasonCode is Booking's PenaltyType enum name (e.g. "LateCancellation", "NoShow").
        var penaltyLabel = p.ReasonCode switch
        {
            "NoShow"           => NotificationMessages.Resolve(locale, "penalty.label.NoShow"),
            "LateCancellation" => NotificationMessages.Resolve(locale, "penalty.label.LateCancellation"),
            _                  => NotificationMessages.Resolve(locale, "penalty.label.generic")
        };
        return NotificationMessages.Resolve(locale, "booking.penaltyApplied.message", penaltyLabel);
    }

    private static string? ResolveNextAction(string eventType) =>
        eventType switch
        {
            "booking.slotAllocated" => "confirmUsage",
            "booking.requestSubmitted" => "cancel",
            _ => null
        };

    public static string DeduplicationKey(string eventId, string recipientId, string notificationType,
        string channel = NotificationChannel.InApp)
        => $"{eventId}:{recipientId}:{notificationType}:{channel}";
}
