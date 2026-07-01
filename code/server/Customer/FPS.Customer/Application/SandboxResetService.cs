using FPS.Customer.Domain;
using FPS.SharedKernel.Infrastructure;

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
    ISandboxResetAudit audit)
{
    public async Task<(SandboxResetSummary? summary, string? error)> ResetAsync(
        string tenantId, string actorHash, string authorizationHeader, CancellationToken ct)
    {
        // ── Guard: fail closed BEFORE any destructive action ──────────────────────────────
        if (string.IsNullOrWhiteSpace(tenantId)) return (null, "Tenant id is required.");

        TenantWorkspace? tenant;
        try { tenant = await repository.GetAsync(tenantId, ct); }
        catch (ArgumentException) { return (null, "Unknown tenant."); } // invalid id shape
        if (tenant is null) return (null, "Unknown tenant.");
        if (tenant.Kind != TenantKind.Sandbox || !tenant.IsResettableSandbox)
            return (null, "Refusing to reset: tenant is not a resettable sandbox.");

        // Fail closed: a destructive reset must never purge (or report success) when it cannot
        // actually rebuild the golden state. #635 shipped only the purge framework, so until the
        // per-service ITenantStorePurger implementations are registered the reset stays inert —
        // it never purges-then-fails-to-reseed, and never returns a fake empty-purge "success".
        // The same slice that registers purgers must also carry the internal (tenant-scoped)
        // reseed path so re-seed auth is proven before purge for platform callers.
        if (!purgers.Any())
            return (null, "unavailable: sandbox reset is not active yet — no tenant-store purgers are registered.");

        await audit.StartedAsync(actorHash, tenantId, ct);
        try
        {
            var purged = await purge.PurgeAsync(tenantId, sandboxReset: true, ct);
            var (seedResult, seedError) = await seed.SeedAsync(tenantId, actorHash, authorizationHeader, ct);
            if (seedError is not null)
            {
                await audit.FailedAsync(actorHash, tenantId, seedError, ct);
                return (null, $"Reset re-seed failed: {seedError}");
            }

            var summary = new SandboxResetSummary(
                tenantId, purged, seedResult!.ProfilesSeeded, seedResult.SlotsSeeded, DateTimeOffset.UtcNow);
            await audit.CompletedAsync(actorHash, tenantId, summary, ct);
            return (summary, null);
        }
        catch (Exception ex)
        {
            await audit.FailedAsync(actorHash, tenantId, ex.Message, ct);
            return (null, "Reset failed; see audit evidence.");
        }
    }
}
