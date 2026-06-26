using System.Collections.Concurrent;
using Dapr.Client;
using FPS.SharedKernel.Identity;

namespace FPS.SharedKernel.Infrastructure;

// Dapr-backed deactivated user store with write-through in-process cache.
//
// Write-through semantics:
//   Deactivate/Reactivate update the in-process cache and write synchronously to
//   Dapr so the change survives service restart. IsDeactivated checks the cache
//   first; on a cache miss (e.g. first request after restart) it falls back to a
//   synchronous Dapr read. ASP.NET Core thread-pool threads have no
//   SynchronizationContext, so the GetAwaiter().GetResult() calls are safe.
//
// Multi-instance note: deactivations written by one instance propagate to other
// instances on their next cache miss for that user. This eventual-consistency
// window is acceptable for an admin action (deactivation is rare, not
// high-frequency).
public sealed class DaprDeactivatedUserStore : IDeactivatedUserStore
{
    private readonly DaprClient daprClient;
    private readonly ConcurrentDictionary<string, bool> cache = new(StringComparer.OrdinalIgnoreCase);
    private const string StoreName = "configstore";

    public DaprDeactivatedUserStore(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public bool IsDeactivated(string tenantId, string userId)
    {
        var key = DeactivatedKey(tenantId, userId);
        if (cache.TryGetValue(key, out var cached))
            return cached;

        var value = daprClient.GetStateAsync<bool>(StoreName, key).GetAwaiter().GetResult();
        cache[key] = value;
        return value;
    }

    public void Deactivate(string tenantId, string userId)
    {
        var key = DeactivatedKey(tenantId, userId);
        cache[key] = true;
        daprClient.SaveStateAsync(StoreName, key, true).GetAwaiter().GetResult();
    }

    public void Reactivate(string tenantId, string userId)
    {
        var key = DeactivatedKey(tenantId, userId);
        cache[key] = false;
        daprClient.SaveStateAsync(StoreName, key, false).GetAwaiter().GetResult();
    }

    private static string DeactivatedKey(string tenantId, string userId)
        => TenantStorageKey.For("deactivated", tenantId, userId);
}
