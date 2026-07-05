using System.Diagnostics;
using System.Text.Json.Nodes;
using FPS.Audit.Domain;
using FPS.SharedKernel.Observability;
using Microsoft.Extensions.Logging;

namespace FPS.Audit.Application;

// AUTH008B (#734) — ingests security audit evidence published to the "security-events" topic (currently
// email-verification outcomes from Profile). The envelope is duplicated here on purpose: Audit must NOT
// reference Profile. ActorHash arrives already pseudonymised (never a raw user id or email) and is stored
// verbatim. No token or email address is ever present in the envelope.
public sealed class SecurityEventAuditHandler(
    IAuditRepository repository,
    ILogger<SecurityEventAuditHandler> logger)
{
    public async Task HandleAsync(SecurityEventEnvelope e, CancellationToken cancellationToken = default)
    {
        TenantTelemetry.SetTenantTag(Activity.Current, e.TenantId);

        var eventType = $"security.{e.Category}";
        // Deterministic id so a redelivery of the same event collapses onto one immutable record.
        var sourceEventId = $"{eventType}.{e.Outcome}:{e.TenantId}:{e.ActorHash}:{e.OccurredAt:o}";

        if (await repository.ExistsAsync(sourceEventId, e.TenantId, cancellationToken))
        {
            logger.LogDebug(
                "Security audit duplicate skipped. TenantId={TenantId} Category={Category} Outcome={Outcome}",
                e.TenantId, e.Category, e.Outcome);
            return;
        }

        var record = new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(),
            SourceEventId = sourceEventId,
            EventType = eventType,
            EventVersion = 1,
            OccurredAt = e.OccurredAt.UtcDateTime,
            RecordedAt = DateTime.UtcNow,
            TenantId = e.TenantId,
            ActorType = "user",
            ActorHash = e.ActorHash, // already pseudonymised by the publisher — do NOT hash again
            Source = "profile",
            EntityType = e.Category,
            EntityId = e.ActorHash,
            Action = $"{eventType}.{e.Outcome}",
            Result = e.Outcome,
            Payload = new JsonObject
            {
                ["category"] = e.Category,
                ["outcome"] = e.Outcome,
                ["reason"] = e.Reason,
            },
            Summary = $"{e.Category} {e.Outcome}",
        };

        await repository.AppendAsync(record, cancellationToken);

        logger.LogInformation(
            "Security audit ingested. TenantId={TenantId} Category={Category} Outcome={Outcome} SourceEventId={SourceEventId}",
            record.TenantId, e.Category, e.Outcome, record.SourceEventId);
    }
}

// Mirror of Profile's SecurityAuditEvent — field-identical so the pub/sub JSON binds. Carries a hashed
// actor plus outcome/reason only; never a token or email address.
public sealed record SecurityEventEnvelope(
    string Category,
    string Outcome,
    string TenantId,
    string ActorHash,
    DateTimeOffset OccurredAt,
    string? Reason);
