using Dapr.Client;
using FPS.Customer.Application;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// PLAT003B — persists the last sandbox-reset outcome per tenant in customerstore (last-writer-wins;
/// evidence is a single latest snapshot, not an append log). Read by the platform evidence endpoint.
/// </summary>
public sealed class DaprSandboxResetEvidenceStore(DaprClient daprClient) : ISandboxResetEvidenceStore
{
    private const string Store = "customerstore";

    public Task RecordAsync(SandboxResetEvidence evidence, CancellationToken ct) =>
        daprClient.SaveStateAsync(Store, CustomerStorageKey.SandboxResetEvidence(evidence.TenantId), evidence, cancellationToken: ct);

    public Task<SandboxResetEvidence?> GetLatestAsync(string tenantId, CancellationToken ct) =>
        daprClient.GetStateAsync<SandboxResetEvidence?>(Store, CustomerStorageKey.SandboxResetEvidence(tenantId), cancellationToken: ct);
}
