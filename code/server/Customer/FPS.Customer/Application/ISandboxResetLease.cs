namespace FPS.Customer.Application;

/// <summary>
/// PLAT003B — per-window dedupe lease so that, across multiple Customer replicas, at most one
/// replica performs the scheduled sandbox reset per schedule window. Backed by the customer state
/// store's ETag compare-and-swap: the first replica to claim the window wins, the rest skip.
///
/// This is best-effort deduplication, not a hard mutex — correctness under a rare first-run race is
/// guaranteed by the reset itself being idempotent (it purges then replace-reseeds the golden
/// snapshot), matching the codebase's established multi-instance strategy for the draw scheduler
/// (deterministic idempotency rather than distributed locking).
/// </summary>
public interface ISandboxResetLease
{
    /// <summary>Attempts to claim the reset window. Returns true only for the single winning caller.</summary>
    Task<bool> TryAcquireAsync(string window, CancellationToken ct);
}
