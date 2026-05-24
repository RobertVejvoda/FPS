using FPS.Audit.Application.Privacy;
using System.Collections.Concurrent;

namespace FPS.Audit.Infrastructure;

public sealed class InMemoryErasureRequestRepository : IErasureRequestRepository
{
    private readonly ConcurrentDictionary<string, ErasureRequest> store = new();

    public Task SaveAsync(ErasureRequest request, CancellationToken cancellationToken = default)
    {
        store[request.ErasureRequestId] = request;
        return Task.CompletedTask;
    }

    public Task<ErasureRequest?> GetAsync(string erasureRequestId, string tenantId, CancellationToken cancellationToken = default)
    {
        store.TryGetValue(erasureRequestId, out var request);
        return Task.FromResult(request?.TenantId == tenantId ? request : null);
    }

    public Task UpdateStatusAsync(
        string erasureRequestId, string tenantId, string status,
        IReadOnlyList<ErasureServiceResult>? serviceResults = null,
        string? blockReason = null,
        DateTime? completedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (!store.TryGetValue(erasureRequestId, out var existing) || existing.TenantId != tenantId)
            return Task.CompletedTask;

        store[erasureRequestId] = existing with
        {
            Status = status,
            ServiceResults = serviceResults ?? existing.ServiceResults,
            BlockReason = blockReason ?? existing.BlockReason,
            CompletedAt = completedAt ?? existing.CompletedAt,
        };
        return Task.CompletedTask;
    }
}
