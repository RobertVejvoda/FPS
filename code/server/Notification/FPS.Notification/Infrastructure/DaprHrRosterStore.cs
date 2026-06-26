using Dapr.Client;
using FPS.Notification.Application;
using FPS.SharedKernel.Infrastructure;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Infrastructure;

public sealed class DaprHrRosterStore : IHrRosterStore
{
    private readonly DaprClient daprClient;
    private readonly ILogger<DaprHrRosterStore> logger;
    private readonly Dictionary<string, IReadOnlyCollection<string>> cache = new(StringComparer.Ordinal);
    private const string StoreName = "notificationstore";
    private const string RegistryKey = "notif-roster-registry:all";

    public DaprHrRosterStore(DaprClient daprClient, ILogger<DaprHrRosterStore> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
        this.logger = logger;
    }

    public IReadOnlyCollection<string> GetHrUserIds(string tenantId)
        => cache.TryGetValue(tenantId, out var users) ? users : [];

    public void Set(string tenantId, IEnumerable<string> hrUserIds)
    {
        var list = hrUserIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
        cache[tenantId] = list;
        _ = PersistAsync(tenantId, list);
    }

    public async Task HydrateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var registry = await daprClient.GetStateAsync<List<string>>(StoreName, RegistryKey, cancellationToken: cancellationToken) ?? [];
            foreach (var tenantId in registry)
            {
                var users = await daprClient.GetStateAsync<List<string>>(StoreName, RosterKey(tenantId), cancellationToken: cancellationToken);
                if (users is not null)
                    cache[tenantId] = users;
            }
            logger.LogInformation("HR roster hydrated from Dapr. Tenants={Count}", registry.Count);
        }
        catch (Exception ex)
        {
            // Dapr sidecar not available at startup (e.g. stale-check, dev without Dapr).
            // Config seeder will populate the in-memory cache instead.
            logger.LogWarning(ex, "HR roster Dapr hydration failed; falling back to configuration seeder.");
        }
    }

    private async Task PersistAsync(string tenantId, List<string> hrUserIds)
    {
        try
        {
            await daprClient.SaveStateAsync(StoreName, RosterKey(tenantId), hrUserIds);

            var registry = await daprClient.GetStateAsync<List<string>>(StoreName, RegistryKey) ?? [];
            if (!registry.Contains(tenantId, StringComparer.Ordinal))
            {
                registry.Add(tenantId);
                await daprClient.SaveStateAsync(StoreName, RegistryKey, registry);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist HR roster for tenant {TenantId}", tenantId);
        }
    }

    private static string RosterKey(string tenantId)
        => TenantStorageKey.For("notif-roster", tenantId, "all");
}
