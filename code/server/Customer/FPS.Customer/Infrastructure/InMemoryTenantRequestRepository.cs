using System.Collections.Concurrent;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

// Evaluation-baseline store for tenant requests. A durable Dapr-backed store is a persist
// follow-up (mirroring how Notification/Audit/Profile evolved); the interface is unchanged.
public sealed class InMemoryTenantRequestRepository : ITenantRequestRepository
{
    private readonly ConcurrentDictionary<string, TenantRequest> store = new(StringComparer.Ordinal);

    public Task SaveAsync(TenantRequest request, CancellationToken ct)
    {
        store[request.RequestId] = request;
        return Task.CompletedTask;
    }

    public Task<TenantRequest?> GetAsync(string requestId, CancellationToken ct) =>
        Task.FromResult(store.TryGetValue(requestId, out var r) ? r : null);

    public Task<IReadOnlyList<TenantRequest>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TenantRequest>>(store.Values.ToList());

    public Task<bool> HasOpenRequestForEmailAsync(string contactEmail, CancellationToken ct) =>
        Task.FromResult(store.Values.Any(r =>
            r.Status == TenantRequestStatus.Requested &&
            string.Equals(r.ContactEmail, contactEmail, StringComparison.OrdinalIgnoreCase)));
}
