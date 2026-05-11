using FPS.Audit.Domain;

namespace FPS.Audit.Application;

public sealed class BookingEventAuditHandler(IAuditRepository repository)
{
    private static readonly IReadOnlyDictionary<string, (string entityType, Func<BookingEventPayload, string?> entityId)> EntityMap =
        new Dictionary<string, (string, Func<BookingEventPayload, string?>)>
        {
            ["booking.requestSubmitted"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.requestRejected"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.slotAllocated"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.requestCancelled"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.penaltyApplied"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.noShowRecorded"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.drawCompleted"] = ("drawAttempt", _ => null),
            ["booking.manualCorrectionApplied"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.usageConfirmed"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.requestExpired"] = ("bookingRequest", p => p.BookingRequestId),
        };

    public async Task HandleAsync(BookingEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (await repository.ExistsAsync(envelope.EventId, cancellationToken))
            return;

        var (entityType, resolveEntityId) = EntityMap.TryGetValue(envelope.EventType, out var mapping)
            ? mapping
            : ("unknown", _ => null);

        var record = new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(),
            SourceEventId = envelope.EventId,
            EventType = envelope.EventType,
            EventVersion = envelope.EventVersion,
            OccurredAt = envelope.OccurredAt,
            RecordedAt = DateTime.UtcNow,
            TenantId = envelope.TenantId,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            ActorType = envelope.ActorType,
            ActorHash = Pseudonymiser.Hash(envelope.ActorId),
            Source = envelope.Source,
            EntityType = entityType,
            EntityId = resolveEntityId(envelope.Payload),
            Payload = Pseudonymiser.SanitisePayload(envelope.Payload)
        };

        await repository.AppendAsync(record, cancellationToken);
    }
}
