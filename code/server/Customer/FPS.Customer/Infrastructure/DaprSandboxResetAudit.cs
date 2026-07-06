using Dapr.Client;
using FPS.Customer.Application;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// PLAT003A — publishes sandbox-reset audit evidence to fairspot-pubsub. The actor is a hash, never a
/// raw user id; no secrets, credentials, or raw payloads are included. Audit-service ingestion of
/// the "tenant-reset-events" topic is a follow-up slice (the emitter is in place here).
/// </summary>
public sealed class DaprSandboxResetAudit(DaprClient dapr) : ISandboxResetAudit
{
    private const string PubSub = "fairspot-pubsub";
    private const string Topic = "tenant-reset-events";

    public Task StartedAsync(string actorHash, string tenantId, CancellationToken ct) =>
        Publish("started", tenantId, actorHash, detail: null, ct);

    public Task CompletedAsync(string actorHash, string tenantId, SandboxResetSummary s, CancellationToken ct) =>
        Publish("completed", tenantId, actorHash,
            detail: $"purged=[{string.Join(",", s.Purged.Select(kv => $"{kv.Key}:{kv.Value}"))}] profiles={s.ProfilesSeeded} slots={s.SlotsSeeded}",
            ct);

    public Task FailedAsync(string actorHash, string tenantId, string reason, CancellationToken ct) =>
        Publish("failed", tenantId, actorHash, detail: reason, ct);

    private Task Publish(string action, string tenantId, string actorHash, string? detail, CancellationToken ct) =>
        dapr.PublishEventAsync(PubSub, Topic,
            new SandboxResetAuditEvent(action, tenantId, actorHash, DateTimeOffset.UtcNow, detail), ct);
}

/// <summary>Sandbox-reset audit event. Actor is a hash; no PII.</summary>
public sealed record SandboxResetAuditEvent(
    string Action, string TenantId, string ActorHash, DateTimeOffset OccurredAt, string? Detail);
