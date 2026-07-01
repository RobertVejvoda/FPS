using FPS.Reporting.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Reporting.Infrastructure;

/// <summary>
/// Tenant-purge hook for Reporting (PLAT003C). Reporting has no durable store, but its in-memory
/// repository holds LIVE per-tenant state while the process runs (metrics rows, fairness rows, and
/// seen-event dedup markers). A reset without restarting Reporting would leave stale Reports/Fairness
/// visible, so the purge clears that tenant's in-memory state. Not immutable evidence.
/// </summary>
public sealed class ReportingTenantStorePurger(IReportingRepository repository) : ITenantStorePurger
{
    public string Service => "reporting";

    public bool IsImmutableEvidence => false;

    public Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
        => repository.PurgeTenantAsync(scope.TenantId, ct);
}
