using FPS.SharedKernel.Infrastructure;

namespace FPS.Reporting.Infrastructure;

/// <summary>
/// Tenant-purge hook for Reporting (PLAT003C). Reporting is an in-memory evaluation stub with no
/// durable per-tenant store, so there is nothing to purge — this exists for evidence symmetry, so
/// the platform purge fan-out has a "reporting" entry. Not immutable evidence.
/// </summary>
public sealed class ReportingTenantStorePurger : ITenantStorePurger
{
    public string Service => "reporting";

    public bool IsImmutableEvidence => false;

    public Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
        => Task.FromResult(0); // Reporting holds no durable per-tenant store; nothing to purge (evidence symmetry)
}
