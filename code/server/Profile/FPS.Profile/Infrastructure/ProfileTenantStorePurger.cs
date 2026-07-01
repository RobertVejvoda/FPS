using FPS.Profile.Application;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Profile.Infrastructure;

/// <summary>
/// Purges all Profile-owned data for a single tenant (PLAT003C). Not immutable evidence,
/// so it runs on a normal tenant purge as well as a sandbox reset.
/// </summary>
public sealed class ProfileTenantStorePurger(IProfileRepository repository) : ITenantStorePurger
{
    public string Service => "profile";

    public bool IsImmutableEvidence => false;

    public async Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
        => await repository.PurgeTenantAsync(scope.TenantId, ct);
}
