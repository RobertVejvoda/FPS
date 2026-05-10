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
        string tenantId, string recipientId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NotificationRecord> results = store.Values
            .Where(r => r.TenantId == tenantId && r.RecipientId == recipientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
        return Task.FromResult(results);
    }
}
