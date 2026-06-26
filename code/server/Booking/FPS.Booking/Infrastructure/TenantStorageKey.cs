// Forwarding shim — canonical implementation is in FPS.SharedKernel.Infrastructure.TenantStorageKey.
// Kept in this namespace so existing Booking infrastructure code resolves the name without changes.
namespace FPS.Booking.Infrastructure;

using FPS.SharedKernel.Infrastructure;

public static class TenantStorageKey
{
    public static string Sanitise(string tenantId) => SharedKernel.Infrastructure.TenantStorageKey.Sanitise(tenantId);
    public static string For(string entityType, string tenantId, string entityId) => SharedKernel.Infrastructure.TenantStorageKey.For(entityType, tenantId, entityId);
    public static string For(string entityType, string tenantId, Guid entityId) => SharedKernel.Infrastructure.TenantStorageKey.For(entityType, tenantId, entityId);
}
