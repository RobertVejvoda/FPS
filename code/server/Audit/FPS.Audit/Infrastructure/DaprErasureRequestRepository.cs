using Dapr.Client;
using FPS.Audit.Application.Privacy;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Audit.Infrastructure;

public sealed class DaprErasureRequestRepository : IErasureRequestRepository
{
    private readonly DaprClient daprClient;
    private const string StoreName = "auditstore";

    public DaprErasureRequestRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task SaveAsync(ErasureRequest request, CancellationToken ct = default)
        => await daprClient.SaveStateAsync(StoreName, ErasureKey(request.TenantId, request.ErasureRequestId), request, cancellationToken: ct);

    public async Task<ErasureRequest?> GetAsync(string erasureRequestId, string tenantId, CancellationToken ct = default)
        => await daprClient.GetStateAsync<ErasureRequest>(StoreName, ErasureKey(tenantId, erasureRequestId), cancellationToken: ct);

    public async Task UpdateStatusAsync(
        string erasureRequestId, string tenantId, string status,
        IReadOnlyList<ErasureServiceResult>? serviceResults = null,
        string? blockReason = null,
        DateTime? completedAt = null,
        CancellationToken ct = default)
    {
        var existing = await GetAsync(erasureRequestId, tenantId, ct);
        if (existing is null) return;

        await SaveAsync(existing with
        {
            Status = status,
            ServiceResults = serviceResults ?? existing.ServiceResults,
            BlockReason = blockReason ?? existing.BlockReason,
            CompletedAt = completedAt ?? existing.CompletedAt,
        }, ct);
    }

    private static string ErasureKey(string tenantId, string erasureRequestId)
        => TenantStorageKey.For("erasure", tenantId, erasureRequestId);
}
