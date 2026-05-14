using FPS.Notification.Domain;

namespace FPS.Notification.Application;

public sealed class BookingEventNotificationHandler(INotificationRepository repository, INotificationBroadcaster broadcaster)
{
    private static readonly IReadOnlyDictionary<string, string> MessageTemplates = new Dictionary<string, string>
    {
        ["booking.requestSubmitted"] = "Your parking request was submitted and is waiting for allocation.",
        ["booking.requestRejected"] = "Your parking request could not be allocated.",
        ["booking.slotAllocated"] = "A parking slot was allocated to your request.",
        ["booking.requestCancelled"] = "Your parking request was cancelled.",
        ["booking.penaltyApplied"] = "A penalty was applied to your parking account.",
        ["booking.noShowRecorded"] = "Your allocated parking slot was not confirmed as used.",
        ["booking.drawCompleted"] = "Parking allocation for your requested time slot is complete.",
        ["booking.manualCorrectionApplied"] = "Your parking request was updated by an authorized administrator.",
        ["booking.usageConfirmed"] = "Your parking usage has been confirmed.",
        ["booking.requestExpired"] = "Your parking request has expired.",
    };

    public async Task HandleAsync(BookingEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var recipients = ResolveRecipients(envelope);
        foreach (var recipientId in recipients)
        {
            var dedupKey = DeduplicationKey(envelope.EventId, recipientId, envelope.EventType);
            if (await repository.ExistsAsync(dedupKey, cancellationToken))
                continue;

            var record = new NotificationRecord
            {
                Id = Guid.NewGuid(),
                DeduplicationKey = dedupKey,
                TenantId = envelope.TenantId,
                RecipientId = recipientId,
                NotificationType = envelope.EventType,
                Channel = NotificationChannel.InApp,
                MessageText = ResolveMessage(envelope),
                RelatedRequestId = envelope.Payload.BookingRequestId,
                RelatedDate = envelope.Payload.Date,
                RelatedTimeSlot = envelope.Payload.TimeSlot,
                LocationId = envelope.Payload.LocationId,
                NextAction = ResolveNextAction(envelope.EventType),
                SourceEventId = envelope.EventId,
                CreatedAt = DateTime.UtcNow
            };

            await repository.SaveAsync(record, cancellationToken);
            // Best-effort — broadcaster failure must not affect persistence
            try { await broadcaster.BroadcastAsync(record, cancellationToken); } catch { }
        }
    }

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
        if (MessageTemplates.TryGetValue(envelope.EventType, out var template))
        {
            if (!string.IsNullOrEmpty(envelope.Payload.ReasonText))
                return $"{template} Reason: {envelope.Payload.ReasonText}";
            return template;
        }
        return $"A booking event occurred: {envelope.EventType}.";
    }

    private static string? ResolveNextAction(string eventType) =>
        eventType switch
        {
            "booking.slotAllocated" => "confirmUsage",
            "booking.requestSubmitted" => "cancel",
            _ => null
        };

    public static string DeduplicationKey(string eventId, string recipientId, string notificationType)
        => $"{eventId}:{recipientId}:{notificationType}:{NotificationChannel.InApp}";
}
