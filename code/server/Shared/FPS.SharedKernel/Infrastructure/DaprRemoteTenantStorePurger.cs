using Dapr.Client;

namespace FPS.SharedKernel.Infrastructure;

/// <summary>
/// A Customer-side <see cref="ITenantStorePurger"/> that delegates to a store-owning service's
/// internal <c>POST /purge/tenant</c> endpoint over Dapr service invocation (the #635 erasure
/// transport). Any RPC or non-success response propagates as an exception so the caller
/// (<c>SandboxResetService</c>) fails closed and skips the reseed rather than proceeding on a
/// partial purge. This is deliberately stricter than the GDPR erasure activities, which swallow
/// per-service failures — a destructive reset must abort, not continue.
/// </summary>
public sealed class DaprRemoteTenantStorePurger(
    DaprClient dapr,
    string appId,
    string service,
    bool isImmutableEvidence,
    string methodPath = "purge/tenant") : ITenantStorePurger
{
    public string Service => service;

    public bool IsImmutableEvidence => isImmutableEvidence;

    public async Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
    {
        var response = await dapr.InvokeMethodAsync<TenantPurgeRequest, TenantPurgeResponse>(
            appId, methodPath, new TenantPurgeRequest(scope.TenantId, sandboxReset), ct);
        return response.Count;
    }
}
