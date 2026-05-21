using System.Collections.Concurrent;

namespace FPS.SharedKernel.Identity;

public sealed class InMemoryTenantIdentityConfigStore : ITenantIdentityConfigStore
{
    private readonly ConcurrentDictionary<string, byte> configured =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnforcementActive => !configured.IsEmpty;
    public bool IsConfigured(string tenantId) => configured.ContainsKey(tenantId);

    public void Register(string tenantId) => configured.TryAdd(tenantId, 0);
    public void Unregister(string tenantId) => configured.TryRemove(tenantId, out _);
}
