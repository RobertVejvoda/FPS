using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.Extensions.Hosting;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// Hydrates the in-memory identity stores from the durable Dapr repository at startup.
/// In non-Development environments, a repository failure propagates as an unhandled exception,
/// crashing the process before app.Run() so the orchestrator can restart the pod once Dapr
/// is available. In Development, the failure is logged and the stores start empty.
/// </summary>
public sealed class IdentityStoreHydrator(
    ITenantIdentityRepository repository,
    InMemoryTenantIdentityConfigStore configStore,
    InMemoryTenantRoleMappingStore roleMappingStore,
    ILogger<IdentityStoreHydrator> logger,
    IHostEnvironment environment)
{
    public async Task HydrateAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> tenantIds;
        try
        {
            tenantIds = await repository.GetConfiguredTenantIdsAsync(ct);
        }
        catch (Exception ex) when (environment.IsDevelopment())
        {
            // Development only: allow the service to start with empty stores so local
            // development works without a Dapr sidecar. In all other profiles the
            // exception propagates — crashing startup is the correct fail-closed behavior.
            logger.LogError(ex, "Identity store hydration failed; starting with empty stores (Development only).");
            return;
        }
        // Non-development: any exception propagates here → process exits → orchestrator restarts.

        foreach (var tenantId in tenantIds)
        {
            var config = await repository.GetConfigAsync(tenantId, ct);
            if (config is null) continue;
            configStore.Register(config.TenantId);
            roleMappingStore.SetMapping(config.TenantId, config.RoleMapping);
            configStore.SetClaimConfig(config.TenantId, new TenantClaimConfig(
                config.TenantClaimName, config.SubjectClaimName, config.RoleClaimNames));
        }

        logger.LogInformation("Identity stores hydrated with {Count} tenant(s).", tenantIds.Count);
    }
}
