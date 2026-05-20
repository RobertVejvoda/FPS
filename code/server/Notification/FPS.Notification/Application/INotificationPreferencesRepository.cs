using FPS.Notification.Domain;

namespace FPS.Notification.Application;

public interface INotificationPreferencesRepository
{
    Task<NotificationPreferences> GetOrDefaultAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task SaveAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default);
}
