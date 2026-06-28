using Dapr.Client;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// Durable system of record for tenant requests, backed by the Customer Dapr state store.
/// Requests are platform-level (a prospect has no tenant yet), so they live under a platform
/// namespace rather than a tenant scope. A small id index supports the operator queue listing
/// (the state store has no native list); the index is maintained read-modify-write, which is
/// safe for the low submission volume of onboarding.
/// </summary>
public sealed class DaprTenantRequestRepository(DaprClient daprClient) : ITenantRequestRepository
{
    private const string Store = "customerstore";
    private const string IndexKey = "tenant-requests:index";
    private const int MaxRetries = 5;

    private static string Key(string requestId) => $"tenant-request:{requestId}";

    public async Task SaveAsync(TenantRequest request, CancellationToken ct)
    {
        await daprClient.SaveStateAsync(Store, Key(request.RequestId), request, cancellationToken: ct);
        await AddToIndexAsync(request.RequestId, ct);
    }

    // ETag compare-and-swap so two concurrent public submissions can't both read the same index,
    // append different ids, and have last-writer-wins drop one — which would hide a request from
    // the operator queue (ListAsync). Mirrors DaprCustomerIdentityRepository.AddToIdentityIndexAsync.
    private async Task AddToIndexAsync(string requestId, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var (existing, etag) = await daprClient.GetStateAndETagAsync<List<string>>(Store, IndexKey, cancellationToken: ct);
            var ids = existing ?? [];
            if (ids.Contains(requestId, StringComparer.Ordinal))
                return;

            var updated = ids.Append(requestId).ToList();
            if (await daprClient.TrySaveStateAsync(Store, IndexKey, updated, etag, cancellationToken: ct))
                return;

            if (attempt < MaxRetries)
                await Task.Delay(20 * attempt, ct);
        }

        throw new InvalidOperationException($"Failed to update tenant-request index after {MaxRetries} attempts.");
    }

    public Task<TenantRequest?> GetAsync(string requestId, CancellationToken ct) =>
        daprClient.GetStateAsync<TenantRequest?>(Store, Key(requestId), cancellationToken: ct);

    public async Task<IReadOnlyList<TenantRequest>> ListAsync(CancellationToken ct)
    {
        var ids = await daprClient.GetStateAsync<List<string>>(Store, IndexKey, cancellationToken: ct) ?? [];
        var requests = new List<TenantRequest>(ids.Count);
        foreach (var id in ids)
        {
            var request = await GetAsync(id, ct);
            if (request is not null) requests.Add(request);
        }
        return requests;
    }

    public async Task<bool> HasOpenRequestForEmailAsync(string contactEmail, CancellationToken ct)
    {
        var all = await ListAsync(ct);
        return all.Any(r =>
            r.Status == TenantRequestStatus.Requested &&
            string.Equals(r.ContactEmail, contactEmail, StringComparison.OrdinalIgnoreCase));
    }
}
