using FPS.Notification.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Application;

public sealed class BookingEventNotificationHandler(
    INotificationRepository repository,
    INotificationBroadcaster broadcaster,
    IEmailNotificationSender emailSender,
    INotificationPreferencesRepository preferencesRepository,
    INotificationAudienceResolver audienceResolver,
    ILogger<BookingEventNotificationHandler> logger)
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

    // Maps ReasonCode values to employee-safe human-readable text.
    // Codes correspond to BookingRejectionCode enum values.
    private static readonly IReadOnlyDictionary<string, string> SafeRejectionReasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["PastDate"]                  = "The requested date has already passed.",
        ["CutOffPassed"]              = "The submission deadline for this time slot has passed.",
        ["DailyCapExceeded"]          = "The maximum number of requests for this day has been reached.",
        ["DuplicateRequest"]          = "You already have an active request for this time slot.",
        ["VehicleConstraintUnmatched"] = "Your vehicle does not meet the requirements for this parking area.",
        ["NoCapacityAvailable"]       = "There are no available spots for this time slot.",
        ["RequestorIneligible"]       = "Your account is not currently eligible to request parking.",
        ["SameDayBookingDisabled"]    = "Same-day parking requests are not enabled.",
        ["NoCapacityForSameDay"]      = "No spots are available for same-day allocation.",
        ["ProfileUnavailable"]        = "Your profile information could not be verified. Please check your account.",
        ["DrawNotSelected"]           = "Your request was not selected in the draw for this time slot.",
    };

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
        if (await repository.ExistsAsync(dedupKey, cancellationToken))
            return;

        var record = CreateRecord(envelope, delivery, NotificationChannel.InApp, dedupKey);
        await repository.SaveAsync(record, cancellationToken);
        // Best-effort — broadcaster failure must not affect persistence
        try { await broadcaster.BroadcastAsync(record, cancellationToken); } catch { }
    }

    private async Task HandleEmailAsync(BookingEventEnvelope envelope, DeliveryTarget delivery, CancellationToken cancellationToken)
    {
        var dedupKey = DeduplicationKey(envelope.EventId, delivery.RecipientId, delivery.EffectiveType, NotificationChannel.Email);
        if (await repository.ExistsAsync(dedupKey, cancellationToken))
            return;

        var record = CreateRecord(envelope, delivery, NotificationChannel.Email, dedupKey);

        EmailSendResult result;
        try { result = await emailSender.SendAsync(record, cancellationToken); }
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
        BookingEventEnvelope envelope, DeliveryTarget delivery, string channel, string dedupKey) => new()
    {
        Id = Guid.NewGuid(),
        DeduplicationKey = dedupKey,
        TenantId = envelope.TenantId,
        RecipientId = delivery.RecipientId,
        NotificationType = delivery.EffectiveType,
        Channel = channel,
        MessageText = ResolveMessage(envelope, delivery.EffectiveType),
        RelatedRequestId = envelope.Payload.BookingRequestId,
        RelatedDate = envelope.Payload.Date,
        RelatedTimeSlot = envelope.Payload.TimeSlot,
        LocationId = envelope.Payload.LocationId,
        NextAction = ResolveNextAction(delivery.EffectiveType),
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

    private static string ResolveMessage(BookingEventEnvelope envelope, string effectiveType)
    {
        var p = envelope.Payload;
        var ctx = BuildContext(p.Date, p.LocationId, p.TimeSlot);

        return effectiveType switch
        {
            "booking.requestSubmitted" =>
                $"Your parking request{ctx} has been submitted and is waiting for draw allocation.",

            "booking.requestSubmitted" + HrSuffix =>
                $"A new parking request{ctx} has been submitted and is awaiting allocation.",

            "booking.requestRejected" =>
                BuildRejectionMessage(p, ctx),

            "booking.slotAllocated" =>
                p.AllocationSource == "reallocation"
                    ? $"A parking spot has been reallocated to your request{ctx} after a cancellation freed a slot."
                    : $"A parking spot has been allocated to your request{ctx}.",

            "booking.requestCancelled" =>
                BuildCancelledMessage(p, envelope.ActorType),

            "booking.requestCancelled" + HrSuffix =>
                BuildHrCancellationMessage(p, ctx),

            "booking.drawCompleted" =>
                BuildDrawCompletedMessage(p, ctx, hrAudience: false),

            "booking.drawCompleted" + HrSuffix =>
                BuildDrawCompletedMessage(p, ctx, hrAudience: true),

            "booking.noShowRecorded" =>
                $"Your parking spot{ctx} was recorded as a no-show. This may affect your future allocation priority.",

            "booking.penaltyApplied" =>
                BuildPenaltyMessage(p),

            "booking.usageConfirmed" =>
                $"Your parking usage{ctx} has been confirmed.",

            "booking.requestExpired" =>
                $"Your parking request{ctx} has expired and is no longer active.",

            "booking.manualCorrectionApplied" =>
                string.IsNullOrEmpty(p.ReasonText)
                    ? "Your parking request was updated by an authorized administrator."
                    : $"Your parking request was updated by an authorized administrator. Reason: {p.ReasonText}",

            _ => $"A booking event occurred: {effectiveType}."
        };
    }

    private static string BuildContext(string? date, string? locationId, string? timeSlot)
    {
        if (string.IsNullOrEmpty(date)) return string.Empty;
        var datePart = TryFormatDate(date);
        var location = string.IsNullOrEmpty(locationId) ? string.Empty : $" at {locationId}";
        var slot = string.IsNullOrEmpty(timeSlot) ? string.Empty : $" ({timeSlot})";
        return $" for {datePart}{location}{slot}";
    }

    private static string TryFormatDate(string date)
    {
        return DateOnly.TryParse(date, out var d)
            ? d.ToString("d MMM yyyy")
            : date;
    }

    private static string BuildRejectionMessage(BookingEventPayload p, string ctx)
    {
        var reason = !string.IsNullOrEmpty(p.ReasonCode) && SafeRejectionReasons.TryGetValue(p.ReasonCode, out var safe)
            ? safe
            : !string.IsNullOrEmpty(p.ReasonText) ? p.ReasonText : null;

        return reason is not null
            ? $"Your parking request{ctx} could not be processed. {reason}"
            : $"Your parking request{ctx} could not be processed.";
    }

    private static string BuildCancelledMessage(BookingEventPayload p, string actorType)
    {
        var ctx = BuildContext(p.Date, p.LocationId, p.TimeSlot);
        var isHr = actorType is "hr_manager" or "admin";
        if (isHr)
        {
            return string.IsNullOrEmpty(p.ReasonText)
                ? $"Your parking request{ctx} was cancelled by HR."
                : $"Your parking request{ctx} was cancelled by HR. Reason: {p.ReasonText}";
        }
        return $"Your parking request{ctx} has been cancelled.";
    }

    private static string BuildDrawCompletedMessage(BookingEventPayload p, string ctx, bool hrAudience)
    {
        if (p.AllocatedCount.HasValue && p.RejectedCount.HasValue)
        {
            var total = p.AllocatedCount.Value + p.RejectedCount.Value + (p.WaitlistedCount ?? 0);
            return hrAudience
                ? $"The parking draw{ctx} has completed: {p.AllocatedCount} of {total} requests allocated."
                : $"Parking allocation{ctx} is complete: {p.AllocatedCount} of {total} requests allocated.";
        }
        return hrAudience
            ? $"The parking draw{ctx} has completed."
            : $"Parking allocation{ctx} is complete.";
    }

    private static string BuildHrCancellationMessage(BookingEventPayload p, string ctx)
    {
        return string.IsNullOrEmpty(p.ReasonText)
            ? $"An employee has cancelled their parking request{ctx}."
            : $"An employee has cancelled their parking request{ctx}. Reason: {p.ReasonText}";
    }

    private static string BuildPenaltyMessage(BookingEventPayload p)
    {
        var penaltyLabel = p.ReasonCode switch
        {
            "NoShow"    => "no-show",
            "LateCancel" => "late cancellation",
            _           => "a booking violation"
        };
        return $"A penalty was applied to your account due to {penaltyLabel}. This may affect your future allocation priority.";
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
