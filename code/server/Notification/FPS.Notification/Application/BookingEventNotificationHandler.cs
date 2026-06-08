using FPS.Notification.Domain;
using Microsoft.Extensions.Logging;
using Dapr.Client;

namespace FPS.Notification.Application;

public sealed class BookingEventNotificationHandler(
    INotificationRepository repository,
    INotificationBroadcaster broadcaster,
    IEmailNotificationSender emailSender,
    INotificationPreferencesRepository preferencesRepository,
    DaprClient daprClient,
    ILogger<BookingEventNotificationHandler> logger)
{
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

        var notificationClass = NotificationClassifier.Classify(envelope.EventType);

        // Fetch tenant policy to determine if usage confirmation is enabled
        var usageConfirmationEnabled = await GetUsageConfirmationEnabledAsync(envelope.TenantId, cancellationToken);

        var recipientCount = 0;
        foreach (var recipientId in ResolveRecipients(envelope))
        {
            var prefs = await preferencesRepository.GetOrDefaultAsync(envelope.TenantId, recipientId, cancellationToken);
            if (!prefs.AllowsDelivery(notificationClass))
            {
                logger.LogDebug(
                    "Notification suppressed by user preference. TenantId={TenantId} NotificationType={NotificationType} Class={Class}",
                    envelope.TenantId, envelope.EventType, notificationClass);
                continue;
            }

            await HandleInAppAsync(envelope, recipientId, usageConfirmationEnabled, cancellationToken);
            await HandleEmailAsync(envelope, recipientId, usageConfirmationEnabled, cancellationToken);
            recipientCount++;
        }

        logger.LogInformation(
            "Notification event processed. TenantId={TenantId} EventType={EventType} SourceEventId={SourceEventId} RecipientCount={RecipientCount}",
            envelope.TenantId, envelope.EventType, envelope.EventId, recipientCount);
    }

    private async Task HandleInAppAsync(BookingEventEnvelope envelope, string recipientId, bool usageConfirmationEnabled, CancellationToken cancellationToken)
    {
        var dedupKey = DeduplicationKey(envelope.EventId, recipientId, envelope.EventType, NotificationChannel.InApp);
        if (await repository.ExistsAsync(dedupKey, cancellationToken))
            return;

        var record = CreateRecord(envelope, recipientId, NotificationChannel.InApp, dedupKey, usageConfirmationEnabled);
        await repository.SaveAsync(record, cancellationToken);
        // Best-effort — broadcaster failure must not affect persistence
        try { await broadcaster.BroadcastAsync(record, cancellationToken); } catch { }
    }

    private async Task HandleEmailAsync(BookingEventEnvelope envelope, string recipientId, bool usageConfirmationEnabled, CancellationToken cancellationToken)
    {
        var dedupKey = DeduplicationKey(envelope.EventId, recipientId, envelope.EventType, NotificationChannel.Email);
        if (await repository.ExistsAsync(dedupKey, cancellationToken))
            return;

        var record = CreateRecord(envelope, recipientId, NotificationChannel.Email, dedupKey, usageConfirmationEnabled);

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
        BookingEventEnvelope envelope, string recipientId, string channel, string dedupKey, bool usageConfirmationEnabled) => new()
    {
        Id = Guid.NewGuid(),
        DeduplicationKey = dedupKey,
        TenantId = envelope.TenantId,
        RecipientId = recipientId,
        NotificationType = envelope.EventType,
        Channel = channel,
        MessageText = ResolveMessage(envelope),
        RelatedRequestId = envelope.Payload.BookingRequestId,
        RelatedDate = envelope.Payload.Date,
        RelatedTimeSlot = envelope.Payload.TimeSlot,
        LocationId = envelope.Payload.LocationId,
        NextAction = ResolveNextAction(envelope.EventType, usageConfirmationEnabled),
        SourceEventId = envelope.EventId,
        CreatedAt = DateTime.UtcNow
    };

    private static IEnumerable<string> ResolveRecipients(BookingEventEnvelope envelope)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(envelope.Payload.RequestorId) &&
            seen.Add(envelope.Payload.RequestorId))
            yield return envelope.Payload.RequestorId;

        if (envelope.Payload.AffectedRecipientIds is { Count: > 0 })
        {
            foreach (var id in envelope.Payload.AffectedRecipientIds)
            {
                if (!string.IsNullOrEmpty(id) && seen.Add(id))
                    yield return id;
            }
        }
    }

    private static string ResolveMessage(BookingEventEnvelope envelope)
    {
        var p = envelope.Payload;
        var ctx = BuildContext(p.Date, p.LocationId, p.TimeSlot);

        return envelope.EventType switch
        {
            "booking.requestSubmitted" =>
                $"Your parking request{ctx} has been submitted and is waiting for draw allocation.",

            "booking.requestRejected" =>
                BuildRejectionMessage(p, ctx),

            "booking.slotAllocated" =>
                p.AllocationSource == "reallocation"
                    ? $"A parking spot has been reallocated to your request{ctx} after a cancellation freed a slot."
                    : $"A parking spot has been allocated to your request{ctx}.",

            "booking.requestCancelled" =>
                BuildCancelledMessage(p, envelope.ActorType),

            "booking.drawCompleted" =>
                BuildDrawCompletedMessage(p),

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

            _ => $"A booking event occurred: {envelope.EventType}."
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

    private static string BuildDrawCompletedMessage(BookingEventPayload p)
    {
        var ctx = BuildContext(p.Date, p.LocationId, p.TimeSlot);
        if (p.AllocatedCount.HasValue && p.RejectedCount.HasValue)
        {
            var total = p.AllocatedCount.Value + p.RejectedCount.Value + (p.WaitlistedCount ?? 0);
            return $"Parking allocation{ctx} is complete: {p.AllocatedCount} of {total} requests allocated.";
        }
        return $"Parking allocation{ctx} is complete.";
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

    private static string? ResolveNextAction(string eventType, bool usageConfirmationEnabled) =>
        eventType switch
        {
            "booking.slotAllocated" => usageConfirmationEnabled ? "confirmUsage" : null,
            "booking.requestSubmitted" => "cancel",
            _ => null
        };

    public static string DeduplicationKey(string eventId, string recipientId, string notificationType,
        string channel = NotificationChannel.InApp)
        => $"{eventId}:{recipientId}:{notificationType}:{channel}";

    private async Task<bool> GetUsageConfirmationEnabledAsync(string tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var policyDto = await daprClient.GetStateAsync<TenantPolicyDto>(
                "configurationstore",
                TenantPolicyKey(tenantId),
                cancellationToken: cancellationToken);
            return policyDto?.UsageConfirmationEnabled ?? false;
        }
        catch
        {
            // If policy cannot be fetched, default to false (usage confirmation disabled)
            return false;
        }
    }

    private static string TenantPolicyKey(string tenantId)
        => $"parking-policy:{SanitiseTenantId(tenantId)}";

    private static string SanitiseTenantId(string tenantId)
        => tenantId.ToLowerInvariant();

    private sealed class TenantPolicyDto
    {
        public bool UsageConfirmationEnabled { get; set; }
    }
}
