using FPS.Notification.Domain;

namespace FPS.Notification.Application;

public interface INotificationBroadcaster
{
    Task BroadcastAsync(NotificationRecord record, CancellationToken cancellationToken = default);
    IAsyncEnumerable<NotificationRecord> SubscribeAsync(string tenantId, string recipientId, CancellationToken cancellationToken = default);
}
