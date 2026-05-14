using FPS.Notification.Domain;

namespace FPS.Notification.Application;

public interface INotificationRepository
{
    Task<bool> ExistsAsync(string deduplicationKey, CancellationToken cancellationToken = default);
    Task SaveAsync(NotificationRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationRecord>> GetByRecipientAsync(
        string tenantId, string recipientId,
        bool unreadOnly = false, string? type = null, int pageSize = 50,
        CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(string tenantId, string recipientId, CancellationToken cancellationToken = default);
    Task<bool> MarkReadAsync(Guid notificationId, string tenantId, string recipientId, CancellationToken cancellationToken = default);
}
