using Dapr.Client;
using FPS.Booking.Application.Services;
using Microsoft.Extensions.Logging;

namespace FPS.Booking.Infrastructure.Services;

// PLAT-seats (#710) — reads a tenant's enabled modules from the Customer service over Dapr service
// invocation (mirrors ConfigurationSlotService's internal-call convention). Fails CLOSED: if the
// lookup fails we treat the tenant as Parking-only, so a Seats request is rejected rather than
// silently accepted when the module boundary can't be confirmed.
public sealed class DaprTenantModulesService(DaprClient daprClient, ILogger<DaprTenantModulesService> logger)
    : ITenantModulesService
{
    private const string CustomerAppId = "fairspot-customer";
    private const string ModulesMethod = "internal/customer/tenant-modules";
    private static readonly IReadOnlyList<string> ParkingOnly = ["Parking"];

    public async Task<IReadOnlyList<string>> GetEnabledModulesAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await daprClient.InvokeMethodAsync<InternalTenantModulesRequest, InternalTenantModulesResponse>(
                CustomerAppId, ModulesMethod, new InternalTenantModulesRequest(tenantId), cancellationToken);
            var modules = response?.EnabledModules;
            return modules is { Count: > 0 } ? modules : ParkingOnly;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tenant modules lookup failed for tenant {TenantId}; treating as Parking-only.", tenantId);
            return ParkingOnly;
        }
    }
}

// Mirror of the Customer service's internal contract (InternalTenantModulesController). Duplicated
// rather than shared, matching the service-invocation convention.
public sealed record InternalTenantModulesRequest(string TenantId);
public sealed record InternalTenantModulesResponse(IReadOnlyList<string> EnabledModules);
