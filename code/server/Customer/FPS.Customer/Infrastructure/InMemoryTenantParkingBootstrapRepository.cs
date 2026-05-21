using System.Collections.Concurrent;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class InMemoryTenantParkingBootstrapRepository : ITenantParkingBootstrapRepository
{
    private readonly ConcurrentDictionary<string, TenantParkingBootstrap> store =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<TenantParkingBootstrap> GetOrCreateAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(store.GetOrAdd(tenantId, id => new TenantParkingBootstrap { TenantId = id }));

    public Task SaveAsync(TenantParkingBootstrap bootstrap, CancellationToken ct)
    {
        store[bootstrap.TenantId] = bootstrap;
        return Task.CompletedTask;
    }
}
