using Dapr.Client;
using FPS.Notification.Application;
using FPS.SharedKernel.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Infrastructure;

public sealed class DaprHrRosterStore : IHrRosterStore
{
    private readonly DaprClient daprClient;
    private readonly ILogger<DaprHrRosterStore> logger;
    private readonly Dictionary<string, IReadOnlyCollection<string>> cache = new(StringComparer.Ordinal);
    private const string StoreName = "notificationstore";
    private const string RegistryKey = "notif-roster-registry:all";

    // True when the most recent Set() could not write to Dapr durably.
    // Exposed so a health check can report Degraded and alert ops.
    public bool IsRosterPersistenceDegraded { get; private set; }

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
        // Block until durable write completes. Set is only called at startup (not on
        // request threads), so GetAwaiter().GetResult() is safe here.
        try
        {
            PersistAsync(tenantId, list).GetAwaiter().GetResult();
            IsRosterPersistenceDegraded = false;
        }
        catch (Exception ex)
        {
            // Roster is in memory but NOT restart-safe. Set the degraded flag so the
            // health gate surfaces the failure without bringing the service down.
            IsRosterPersistenceDegraded = true;
            logger.LogError(ex, "HR roster durable write failed for tenant {TenantId}. Roster is NOT restart-safe.", tenantId);
        }
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
        // No try/catch — exceptions propagate to Set() which owns error classification.
        await daprClient.SaveStateAsync(StoreName, RosterKey(tenantId), hrUserIds);

        var registry = await daprClient.GetStateAsync<List<string>>(StoreName, RegistryKey) ?? [];
        if (!registry.Contains(tenantId, StringComparer.Ordinal))
        {
            registry.Add(tenantId);
            await daprClient.SaveStateAsync(StoreName, RegistryKey, registry);
        }
    }

    private static string RosterKey(string tenantId)
        => TenantStorageKey.For("notif-roster", tenantId, "all");
}

/// <summary>
/// Reports Degraded when DaprHrRosterStore could not write the roster durably.
/// Ops must investigate Dapr sidecar connectivity before restarting the service.
/// </summary>
public sealed class HrRosterPersistenceHealthCheck(DaprHrRosterStore store) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.IsRosterPersistenceDegraded
            ? HealthCheckResult.Degraded("HR roster durable write failed. Roster is NOT restart-safe — Dapr sidecar may be unavailable.")
            : HealthCheckResult.Healthy("HR roster persistence is healthy."));
    }
}
