using System.Collections.Concurrent;

namespace FPS.SharedKernel.Identity;

public sealed record TenantClaimConfig(
    string TenantClaimName,
    string SubjectClaimName,
    IReadOnlyList<string> RoleClaimNames);

public sealed class InMemoryTenantIdentityConfigStore : ITenantIdentityConfigStore
{
    private readonly ConcurrentDictionary<string, byte> configured =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, TenantClaimConfig> claimConfigs =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnforcementActive => !configured.IsEmpty;
    public bool IsConfigured(string tenantId) => configured.ContainsKey(tenantId);

    public void Register(string tenantId) => configured.TryAdd(tenantId, 0);
    public void Unregister(string tenantId)
    {
        configured.TryRemove(tenantId, out _);
        claimConfigs.TryRemove(tenantId, out _);
    }

    public void SetClaimConfig(string tenantId, TenantClaimConfig config) =>
        claimConfigs[tenantId] = config;

    public TenantClaimConfig? GetClaimConfig(string tenantId) =>
        claimConfigs.TryGetValue(tenantId, out var c) ? c : null;
}
