using FPS.Audit.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Audit.Infrastructure;

/// <summary>
/// Purges a single tenant's audit evidence. Audit records are immutable evidence, so this purger
/// only deletes on an explicit sandbox/demo reset — a normal GDPR erasure must never clear them.
/// The orchestrator already skips immutable-evidence purgers unless <c>sandboxReset</c>; this purger
/// self-gates as well (defense in depth).
/// </summary>
public sealed class AuditTenantStorePurger(IAuditRetentionRepository repository) : ITenantStorePurger
{
    public string Service => "audit";

    public bool IsImmutableEvidence => true;

    public async Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
    {
        if (!sandboxReset)
            return 0; // defense in depth: audit evidence is only cleared on a sandbox reset

        return await repository.PurgeTenantAsync(scope.TenantId, ct);
    }
}
