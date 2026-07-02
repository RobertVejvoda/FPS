using System.Diagnostics;
using System.Text.Json.Nodes;
using FPS.Audit.Domain;
using FPS.SharedKernel.Observability;
using Microsoft.Extensions.Logging;

namespace FPS.Audit.Application;

// PLAT003C-C2 — ingests sandbox-reset audit evidence published by the Customer
// service to the "tenant-reset-events" topic. The envelope shape is duplicated
// here on purpose: Audit must NOT reference Customer. ActorHash arrives already
// hashed (never a raw user id) — it is stored verbatim, never re-hashed.
public sealed class SandboxResetAuditHandler(
    IAuditRepository repository,
    ILogger<SandboxResetAuditHandler> logger)
{
    private const string EventTypeName = "platform.sandboxReset";

    public async Task HandleAsync(TenantResetEventEnvelope e, CancellationToken cancellationToken = default)
    {
        // PLAT005B — tag the event-processing span with the trusted envelope tenant. No PII.
        TenantTelemetry.SetTenantTag(Activity.Current, e.TenantId);

        // Deterministic id so a redelivery of the same event (same tenant,
        // action, and instant) collapses onto one immutable record.
        var sourceEventId = $"platform.sandboxReset:{e.TenantId}:{e.Action}:{e.OccurredAt:o}";

        if (await repository.ExistsAsync(sourceEventId, e.TenantId, cancellationToken))
        {
            logger.LogDebug(
                "Sandbox reset audit duplicate skipped. TenantId={TenantId} Action={Action} SourceEventId={SourceEventId}",
                e.TenantId, e.Action, sourceEventId);
            return;
        }

        var record = new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(),
            SourceEventId = sourceEventId,
            EventType = EventTypeName,
            EventVersion = 1,
            OccurredAt = e.OccurredAt.UtcDateTime,
            RecordedAt = DateTime.UtcNow,
            TenantId = e.TenantId,
            ActorType = "system",
            ActorHash = e.ActorHash, // already hashed by the publisher — do NOT hash again
            Source = "platform",
            EntityType = "sandboxReset",
            EntityId = e.TenantId,
            Action = $"platform.sandboxReset.{e.Action}",
            Result = e.Action,
            Payload = new JsonObject
            {
                ["action"] = e.Action,
                ["detail"] = e.Detail,
            },
            Summary = $"Sandbox reset {e.Action} for tenant {e.TenantId}",
        };

        await repository.AppendAsync(record, cancellationToken);

        logger.LogInformation(
            "Sandbox reset audit ingested. TenantId={TenantId} Action={Action} SourceEventId={SourceEventId}",
            record.TenantId, e.Action, record.SourceEventId);
    }
}

// Mirror of Customer's SandboxResetAuditEvent — kept field-identical so the
// pub/sub JSON binds. Audit deliberately does not reference the Customer project.
public sealed record TenantResetEventEnvelope(
    string Action,
    string TenantId,
    string ActorHash,
    DateTimeOffset OccurredAt,
    string? Detail);
