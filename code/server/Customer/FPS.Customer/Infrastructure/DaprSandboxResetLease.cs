using Dapr.Client;
using FPS.Customer.Application;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// PLAT003B — distributed reset-window lease via customerstore ETag compare-and-swap (mirrors the
/// tenant-index CAS in <see cref="DaprCustomerTenantRepository"/>). The stored value is the window key
/// currently claimed; a replica wins the window only if its CAS write lands against the ETag it observed,
/// so two replicas that observe an already-claimed window both skip. A rare first-run race is tolerated
/// because the reset is idempotent (see <see cref="ISandboxResetLease"/>).
/// </summary>
public sealed class DaprSandboxResetLease(DaprClient daprClient) : ISandboxResetLease
{
    private const string Store = "customerstore";

    public async Task<bool> TryAcquireAsync(string window, CancellationToken ct)
    {
        var key = CustomerStorageKey.SandboxResetLease();
        var (current, etag) = await daprClient.GetStateAndETagAsync<string>(Store, key, cancellationToken: ct);

        // Already claimed for this window (by this or another replica) → skip without resetting again.
        if (string.Equals(current, window, StringComparison.Ordinal))
            return false;

        // Claim the window: only the replica whose write lands against the observed ETag wins.
        return await daprClient.TrySaveStateAsync(Store, key, window, etag, cancellationToken: ct);
    }
}
