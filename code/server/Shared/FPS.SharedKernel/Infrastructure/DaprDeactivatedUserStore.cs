using System.Collections.Concurrent;
using Dapr.Client;
using FPS.SharedKernel.Identity;

namespace FPS.SharedKernel.Infrastructure;

// Dapr-backed deactivated user store with a 30-second write-through cache.
//
// Store: "deactivatedstore" Dapr component (no scope restriction — shared by all fps-* services).
// Key format: deactivated:{tenantId}:{userId} → bool
//
// Cache TTL semantics:
//   Entries are cached for 30 seconds. After expiry the next IsDeactivated call re-reads from Dapr.
//   This bounds cross-instance staleness to ≤30 seconds: if instance A deactivates a user, any
//   other instance will see the change within 30 seconds (once its cached entry expires and it
//   re-reads Dapr). Deactivate/Reactivate update the local cache immediately and write to Dapr
//   synchronously so the change is durable before returning.
//
// Sync-over-async: ASP.NET Core thread-pool threads have no SynchronizationContext, so
// GetAwaiter().GetResult() does not deadlock.
public sealed class DaprDeactivatedUserStore : IDeactivatedUserStore
{
    private readonly DaprClient daprClient;
    private readonly string storeName;
    private readonly ConcurrentDictionary<string, (bool Value, long ExpiresAt)> cache = new();

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public DaprDeactivatedUserStore(DaprClient daprClient, string storeName = "deactivatedstore")
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        this.daprClient = daprClient;
        this.storeName = storeName;
    }

    public bool IsDeactivated(string tenantId, string userId)
    {
        var key = DeactivatedKey(tenantId, userId);
        var now = Environment.TickCount64;

        if (cache.TryGetValue(key, out var entry) && entry.ExpiresAt > now)
            return entry.Value;

        var value = daprClient.GetStateAsync<bool>(storeName, key).GetAwaiter().GetResult();
        cache[key] = (value, now + (long)CacheTtl.TotalMilliseconds);
        return value;
    }

    public void Deactivate(string tenantId, string userId)
    {
        var key = DeactivatedKey(tenantId, userId);
        cache[key] = (true, Environment.TickCount64 + (long)CacheTtl.TotalMilliseconds);
        daprClient.SaveStateAsync(storeName, key, true).GetAwaiter().GetResult();
    }

    public void Reactivate(string tenantId, string userId)
    {
        var key = DeactivatedKey(tenantId, userId);
        cache[key] = (false, Environment.TickCount64 + (long)CacheTtl.TotalMilliseconds);
        daprClient.SaveStateAsync(storeName, key, false).GetAwaiter().GetResult();
    }

    private static string DeactivatedKey(string tenantId, string userId)
        => TenantStorageKey.For("deactivated", tenantId, userId);
}
