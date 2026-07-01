namespace FPS.Customer.Application;

/// <summary>
/// PLAT003B — last-known outcome of a sandbox reset for one tenant, for platform-operator evidence.
/// Contains no PII and no secrets: the actor is a hash, counts are aggregate, and no raw user ids or
/// credentials are stored. A single latest snapshot per tenant (not an append log).
/// </summary>
public sealed record SandboxResetEvidence(
    string TenantId,
    string Status,            // Succeeded | Failed | Unavailable
    string Source,            // manual | scheduled
    string ActorHash,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? SnapshotVersion,
    string? FailureReason,
    IReadOnlyDictionary<string, int>? Purged);

/// <summary>Persists and reads the last sandbox-reset outcome per tenant.</summary>
public interface ISandboxResetEvidenceStore
{
    Task RecordAsync(SandboxResetEvidence evidence, CancellationToken ct);
    Task<SandboxResetEvidence?> GetLatestAsync(string tenantId, CancellationToken ct);
}
