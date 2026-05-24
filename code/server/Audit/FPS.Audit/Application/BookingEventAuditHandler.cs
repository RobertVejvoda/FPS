using System.Diagnostics;
using FPS.Audit.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Audit.Application;

public sealed class BookingEventAuditHandler(IAuditRepository repository, ILogger<BookingEventAuditHandler> logger)
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
            ["booking.drawStarted"] = ("drawAttempt", p => DrawAttemptId(p)),
            ["booking.drawCompleted"] = ("drawAttempt", p => DrawAttemptId(p)),
            ["booking.drawFailed"] = ("drawAttempt", p => DrawAttemptId(p)),
            ["booking.manualCorrectionApplied"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.usageConfirmed"] = ("bookingRequest", p => p.BookingRequestId),
            ["booking.requestExpired"] = ("bookingRequest", p => p.BookingRequestId),
        };

    private static string? DrawAttemptId(BookingEventPayload p) =>
        p.AdditionalData?.TryGetValue("drawAttemptId", out var el) == true ? el.GetString() : null;

    public async Task HandleAsync(BookingEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (await repository.ExistsAsync(envelope.EventId, cancellationToken))
        {
            logger.LogDebug(
                "Audit event duplicate skipped. TenantId={TenantId} EventType={EventType} SourceEventId={SourceEventId}",
                envelope.TenantId, envelope.EventType, envelope.EventId);
            return;
        }

        var (entityType, resolveEntityId) = EntityMap.TryGetValue(envelope.EventType, out var mapping)
            ? mapping
            : ("unknown", _ => null);

        var result = BusinessActivityMapper.ToResult(envelope.EventType);
        var reasonCode = envelope.Payload.ReasonCode;
        var processingActivity = Activity.Current;

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
            Payload = Pseudonymiser.SanitisePayload(envelope.Payload),
            Action = envelope.EventType,
            Result = result,
            ReasonCode = reasonCode,
            Summary = BusinessActivityMapper.ToSummary(envelope.EventType, entityType, result, reasonCode),
            ProcessingTraceId = processingActivity?.TraceId.ToString(),
            SpanId = processingActivity?.SpanId.ToString(),
        };

        await repository.AppendAsync(record, cancellationToken);

        logger.LogInformation(
            "Audit event ingested. TenantId={TenantId} EventType={EventType} SourceEventId={SourceEventId} EntityType={EntityType}",
            record.TenantId, record.EventType, record.SourceEventId, record.EntityType);
    }
}
