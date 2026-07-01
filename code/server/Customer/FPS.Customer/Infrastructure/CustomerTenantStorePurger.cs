using FPS.SharedKernel.Infrastructure;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// Customer's own tenant-store purger for a sandbox reset. Intentionally a no-op: the Customer
/// store holds the tenant's control-plane records — the <c>TenantWorkspace</c>, identity config,
/// and tenant-admin — which MUST survive a reset (they are what keep the sandbox resettable and
/// loginable). A reset clears the tenant's runtime data in the other services (profiles, slots,
/// bookings, audit, …) and reseeds the golden dataset; nothing in the Customer store is tenant
/// runtime data, so there is nothing to purge here. It is registered for evidence symmetry and so
/// the fan-out has a customer entry.
/// </summary>
public sealed class CustomerTenantStorePurger : ITenantStorePurger
{
    public string Service => "customer";

    public bool IsImmutableEvidence => false;

    public Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
        => Task.FromResult(0);
}
