using FPS.SharedKernel.Infrastructure;

namespace FPS.Configuration.Infrastructure;

/// <summary>
/// Purges all Configuration-owned data for a single tenant (PLAT003C). Not immutable evidence,
/// so it runs on a normal tenant purge as well as a sandbox reset.
/// </summary>
public sealed class ConfigurationTenantStorePurger(IConfigurationTenantPurger purger) : ITenantStorePurger
{
    public string Service => "configuration";

    public bool IsImmutableEvidence => false;

    public async Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
        => await purger.PurgeTenantAsync(scope.TenantId, ct);
}
