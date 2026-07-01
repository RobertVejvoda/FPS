using FPS.SharedKernel.Time;
using Microsoft.Extensions.Logging;

namespace FPS.Customer.Application;

/// <summary>Per-target outcome of a scheduled reset tick (no PII).</summary>
public sealed record ScheduledResetOutcome(string TenantId, string Status);

/// <summary>
/// PLAT003B — drives the nightly Green Logistics sandbox reset from a Dapr cron tick.
///
/// Flow: if disabled → skip; otherwise claim a per-window lease so that at most one replica resets per
/// schedule window (multiple replicas receive the same cron tick). The winning replica resets each
/// configured target through <see cref="SandboxResetService"/>, whose guard re-verifies the resettable-
/// sandbox flag from stored metadata — so a misconfigured non-sandbox target is refused without any purge.
///
/// Missed-run behavior: the window key is the UTC date, so re-ticks within a day are no-ops (idempotent);
/// a night the job does not run is simply skipped (no catch-up), and the gap is visible to operators via
/// the last-reset evidence timestamp.
/// </summary>
public sealed class ScheduledSandboxResetService(
    SandboxResetService reset,
    ISandboxResetLease lease,
    SandboxResetSchedulerOptions options,
    ISystemClock clock,
    ILogger<ScheduledSandboxResetService> logger)
{
    // Non-PII synthetic actor recorded on scheduled resets (the audit/evidence stores expect a hash-like
    // opaque actor; "system:..." is safe — it identifies the scheduler, not a person).
    private const string ScheduledActor = "system:sandbox-reset-scheduler";

    public async Task<IReadOnlyList<ScheduledResetOutcome>> RunDueResetsAsync(CancellationToken ct)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Scheduled sandbox reset is disabled; skipping.");
            return [new ScheduledResetOutcome(string.Empty, "Disabled")];
        }
        if (options.Targets.Count == 0)
        {
            logger.LogWarning("Scheduled sandbox reset is enabled but no targets are configured.");
            return [];
        }

        var window = clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd");
        if (!await lease.TryAcquireAsync(window, ct))
        {
            logger.LogInformation(
                "Scheduled sandbox reset window {Window} already claimed by another replica; skipping.", window);
            return [new ScheduledResetOutcome(string.Empty, "Skipped")];
        }

        var outcomes = new List<ScheduledResetOutcome>(options.Targets.Count);
        foreach (var tenantId in options.Targets)
        {
            // authorizationHeader is empty: a scheduled reset has no operator token. While the reset is
            // inert (default off) this never reaches the reseed; the activation slice (PLAT003C) wires an
            // internal tenant-scoped reseed path so scheduled resets never forward an operator token.
            // Use the reset's own classification — never re-derive it from the error string. A
            // Refused is a pre-purge guard rejection; a Failed is a mid-flow purge/reseed failure
            // (e.g. a store purge throwing) after the guard has already passed. Conflating them makes
            // the evidence and the gate diagnosis misleading.
            var (_, error, resetStatus) = await reset.ResetAsync(tenantId, ScheduledActor, source: "scheduled", authorizationHeader: string.Empty, ct);
            var status = resetStatus.ToString();
            if (error is null)
                logger.LogInformation("Scheduled sandbox reset: tenant={TenantId} status={Status}", tenantId, status);
            else
                logger.LogWarning("Scheduled sandbox reset: tenant={TenantId} status={Status} detail={Detail}", tenantId, status, error);
            outcomes.Add(new ScheduledResetOutcome(tenantId, status));
        }
        return outcomes;
    }
}
