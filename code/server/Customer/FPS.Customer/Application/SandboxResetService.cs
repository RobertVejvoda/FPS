using FPS.Customer.Domain;
using FPS.SharedKernel.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace FPS.Customer.Application;

/// <summary>Result summary of a completed sandbox reset (counts only — no PII).</summary>
public sealed record SandboxResetSummary(
    string TenantId,
    IReadOnlyDictionary<string, int> Purged,
    int ProfilesSeeded,
    int SlotsSeeded,
    DateTimeOffset CompletedAt);

/// <summary>Emits sandbox-reset audit evidence (actor hash, target tenant, action, outcome).</summary>
public interface ISandboxResetAudit
{
    Task StartedAsync(string actorHash, string tenantId, CancellationToken ct);
    Task CompletedAsync(string actorHash, string tenantId, SandboxResetSummary summary, CancellationToken ct);
    Task FailedAsync(string actorHash, string tenantId, string reason, CancellationToken ct);
}

/// <summary>
/// PLAT003A — platform-only "reset the evaluation sandbox to its golden state" orchestration.
///
/// Safety-critical guard: the reset aborts BEFORE any purge for a missing/unknown tenant or a
/// tenant that is not a resettable sandbox (<see cref="TenantWorkspace.IsResettableSandbox"/> AND
/// <see cref="TenantKind.Sandbox"/>). The sandbox status is read only from stored tenant metadata,
/// never from the request, so a real customer tenant can never be reset through this path.
///
/// Sequence (only after the guard passes): audit "started" -> reuse the #635 tenant-scoped purge
/// orchestrator (sandboxReset=true, which also clears immutable-evidence stores) -> re-seed the
/// golden Green Logistics snapshot via <see cref="TenantDemoSeedService"/> -> audit
/// "completed"/"failed". The reseed is idempotent (it replaces profiles/config), so repeated
/// resets do not accumulate duplicate data.
/// </summary>
public sealed class SandboxResetService(
    ITenantRepository repository,
    TenantPurgeOrchestrator purge,
    IEnumerable<ITenantStorePurger> purgers,
    TenantDemoSeedService seed,
    ISandboxResetAudit audit,
    ISandboxResetEvidenceStore evidence,
    IConfiguration configuration)
{
    /// <summary>
    /// Resets a resettable sandbox to its golden state. <paramref name="source"/> ("manual" | "scheduled")
    /// is recorded on the operator evidence. Evidence is written for every outcome that reaches a legitimate
    /// resettable sandbox (Succeeded/Failed/Unavailable) — never for a non-sandbox guard rejection, which is a
    /// security refusal, not a reset.
    /// </summary>
    public async Task<(SandboxResetSummary? summary, string? error, SandboxResetStatus status)> ResetAsync(
        string tenantId, string actorHash, string source, string authorizationHeader, CancellationToken ct)
    {
        // ── Guard: fail closed BEFORE any destructive action ──────────────────────────────
        // A guard rejection is a security refusal (Refused), distinct from a mid-flow purge/reseed
        // failure (Failed) once the guard has passed — callers must not conflate the two.
        if (string.IsNullOrWhiteSpace(tenantId)) return (null, "Tenant id is required.", SandboxResetStatus.Refused);

        TenantWorkspace? tenant;
        try { tenant = await repository.GetAsync(tenantId, ct); }
        catch (ArgumentException) { return (null, "Unknown tenant.", SandboxResetStatus.Refused); } // invalid id shape
        if (tenant is null) return (null, "Unknown tenant.", SandboxResetStatus.Refused);
        if (tenant.Kind != TenantKind.Sandbox || !tenant.IsResettableSandbox)
            return (null, "Refusing to reset: tenant is not a resettable sandbox.", SandboxResetStatus.Refused);

        // Past the guard: this is a legitimate resettable sandbox, so every outcome from here is recorded
        // as operator evidence (no PII/secrets — actor hash + aggregate counts only).
        var startedAt = DateTimeOffset.UtcNow;

        // Fail closed until the reset is EXPLICITLY activated (default off). Activation requires
        // BOTH an explicit opt-in (SandboxReset:Enabled) — the operator asserting the internal
        // tenant-scoped reseed path and durable audit ingestion are wired — AND at least one
        // registered ITenantStorePurger. So the destructive path can never turn on merely as a
        // side effect of adding a purger, and never purges-then-fails-to-reseed or returns a fake
        // empty-purge "success". #635 shipped only the purge framework; the slice that registers
        // purgers + internal reseed + audit ingestion flips SandboxReset:Enabled on together.
        var enabled = configuration.GetValue<bool>("SandboxReset:Enabled");
        if (!enabled || !purgers.Any())
        {
            const string unavailable = "unavailable: sandbox reset is not active yet (requires explicit SandboxReset:Enabled plus registered tenant-store purgers).";
            await evidence.RecordAsync(new SandboxResetEvidence(
                tenantId, "Unavailable", source, actorHash, startedAt, DateTimeOffset.UtcNow,
                SnapshotVersion: null, FailureReason: unavailable, Purged: null), ct);
            return (null, unavailable, SandboxResetStatus.Unavailable);
        }

        await audit.StartedAsync(actorHash, tenantId, ct);
        try
        {
            var purged = await purge.PurgeAsync(tenantId, sandboxReset: true, ct);
            var (seedResult, seedError) = await seed.SeedAsync(tenantId, actorHash, authorizationHeader, ct);
            if (seedError is not null)
            {
                await audit.FailedAsync(actorHash, tenantId, seedError, ct);
                await evidence.RecordAsync(new SandboxResetEvidence(
                    tenantId, "Failed", source, actorHash, startedAt, DateTimeOffset.UtcNow,
                    SnapshotVersion: null, FailureReason: seedError, Purged: purged), ct);
                return (null, $"Reset re-seed failed: {seedError}", SandboxResetStatus.Failed);
            }

            var summary = new SandboxResetSummary(
                tenantId, purged, seedResult!.ProfilesSeeded, seedResult.SlotsSeeded, DateTimeOffset.UtcNow);
            await audit.CompletedAsync(actorHash, tenantId, summary, ct);
            await evidence.RecordAsync(new SandboxResetEvidence(
                tenantId, "Succeeded", source, actorHash, startedAt, summary.CompletedAt,
                SnapshotVersion: seedResult.DatasetVersion, FailureReason: null, Purged: purged), ct);
            return (summary, null, SandboxResetStatus.Succeeded);
        }
        catch (Exception ex)
        {
            await audit.FailedAsync(actorHash, tenantId, ex.Message, ct);
            await evidence.RecordAsync(new SandboxResetEvidence(
                tenantId, "Failed", source, actorHash, startedAt, DateTimeOffset.UtcNow,
                SnapshotVersion: null, FailureReason: ex.Message, Purged: null), ct);
            return (null, "Reset failed; see audit evidence.", SandboxResetStatus.Failed);
        }
    }
}

/// <summary>
/// Outcome of a sandbox reset. <see cref="Refused"/> is a pre-purge guard rejection (unknown or
/// non-resettable-sandbox tenant — a security refusal); <see cref="Failed"/> is a mid-flow purge or
/// reseed failure after the guard has already passed; <see cref="Unavailable"/> is the inert state
/// (not activated). These must not be conflated in operator-facing evidence or gate diagnostics.
/// </summary>
public enum SandboxResetStatus
{
    Succeeded,
    Refused,
    Unavailable,
    Failed,
}
