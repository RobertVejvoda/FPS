using FPS.Notification.Application;
using FPS.Notification.Domain;
using System.Collections.Concurrent;

namespace FPS.Notification.Infrastructure;

// Phase 1 stub — replace with Dapr state store / MongoDB read-side.
public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<string, NotificationRecord> store = new();

    public Task<bool> ExistsAsync(string deduplicationKey, CancellationToken cancellationToken = default)
        => Task.FromResult(store.ContainsKey(deduplicationKey));

    public Task SaveAsync(NotificationRecord record, CancellationToken cancellationToken = default)
    {
        store.TryAdd(record.DeduplicationKey, record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationRecord>> GetByRecipientAsync(
        string tenantId, string recipientId,
        bool unreadOnly = false, string? type = null, int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = store.Values
            .Where(r => r.TenantId == tenantId && r.RecipientId == recipientId);

        if (unreadOnly)
            query = query.Where(r => !r.IsRead);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(r => r.NotificationType.StartsWith(type + ".", StringComparison.OrdinalIgnoreCase)
                                  || r.NotificationType.Equals(type, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<NotificationRecord> results = query
            .OrderByDescending(r => r.CreatedAt)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<int> GetUnreadCountAsync(string tenantId, string recipientId, CancellationToken cancellationToken = default)
    {
        int count = store.Values.Count(r =>
            r.TenantId == tenantId &&
            r.RecipientId == recipientId &&
            !r.IsRead);
        return Task.FromResult(count);
    }

    public Task<bool> MarkReadAsync(Guid notificationId, string tenantId, string recipientId, CancellationToken cancellationToken = default)
    {
        var record = store.Values.FirstOrDefault(r =>
            r.Id == notificationId &&
            r.TenantId == tenantId &&
            r.RecipientId == recipientId);

        if (record is null) return Task.FromResult(false);
        record.MarkRead();
        return Task.FromResult(true);
    }
}
