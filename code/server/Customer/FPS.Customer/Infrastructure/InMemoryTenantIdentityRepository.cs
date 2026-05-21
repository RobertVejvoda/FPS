using System.Collections.Concurrent;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class InMemoryTenantIdentityRepository : ITenantIdentityRepository
{
    private readonly ConcurrentDictionary<string, TenantIdentityConfig> configs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<TenantAdminRecord>> admins =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<TenantIdentityConfig?> GetConfigAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(configs.TryGetValue(tenantId, out var c) ? c : null);

    public Task SaveConfigAsync(TenantIdentityConfig config, CancellationToken ct)
    {
        configs[config.TenantId] = config;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TenantAdminRecord>> GetAdminsAsync(string tenantId, CancellationToken ct)
    {
        var list = admins.TryGetValue(tenantId, out var a) ? a : [];
        return Task.FromResult<IReadOnlyList<TenantAdminRecord>>(list.ToList());
    }

    public Task SaveAdminAsync(TenantAdminRecord admin, CancellationToken ct)
    {
        var list = admins.GetOrAdd(admin.TenantId, _ => []);
        lock (list) { list.Add(admin); }
        return Task.CompletedTask;
    }
}
