using FPS.Notification.Domain;

namespace FPS.Notification.Application;

public interface INotificationPreferencesRepository
{
    Task<NotificationPreferences> GetOrDefaultAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task SaveAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default);

    /// <summary>DESTRUCTIVE (PLAT003C): deletes every stored preference for a tenant; returns the count removed.</summary>
    Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
