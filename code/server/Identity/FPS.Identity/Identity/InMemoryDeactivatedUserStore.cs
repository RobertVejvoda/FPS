using System.Collections.Concurrent;

namespace FPS.Identity.Identity;

public sealed class InMemoryDeactivatedUserStore : IDeactivatedUserStore
{
    private readonly ConcurrentDictionary<string, bool> store = new(StringComparer.OrdinalIgnoreCase);

    private static string Key(string tenantId, string userId) => $"{tenantId}:{userId}";

    public bool IsDeactivated(string tenantId, string userId)
        => store.TryGetValue(Key(tenantId, userId), out var v) && v;

    public void Deactivate(string tenantId, string userId)
        => store[Key(tenantId, userId)] = true;

    public void Reactivate(string tenantId, string userId)
        => store[Key(tenantId, userId)] = false;
}
