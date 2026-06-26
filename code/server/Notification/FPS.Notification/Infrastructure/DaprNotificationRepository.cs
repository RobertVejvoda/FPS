using Dapr.Client;
using FPS.Notification.Application;
using FPS.Notification.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Notification.Infrastructure;

public sealed class DaprNotificationRepository : INotificationRepository
{
    private readonly DaprClient daprClient;
    private const string StoreName = "notificationstore";

    public DaprNotificationRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<bool> ExistsAsync(string deduplicationKey, string tenantId, CancellationToken cancellationToken = default)
        => await daprClient.GetStateAsync<bool>(StoreName, DedupKey(tenantId, deduplicationKey), cancellationToken: cancellationToken);

    public async Task SaveAsync(NotificationRecord record, CancellationToken cancellationToken = default)
    {
        var dedupKey = DedupKey(record.TenantId, record.DeduplicationKey);
        if (await daprClient.GetStateAsync<bool>(StoreName, dedupKey, cancellationToken: cancellationToken))
            return;

        // Write order: record → index → dedup marker (so a failed index write leaves the record re-retriable)
        await daprClient.SaveStateAsync(StoreName, RecordKey(record.TenantId, record.RecipientId, record.Id), record, cancellationToken: cancellationToken);
        await AddToIndexAsync(record.TenantId, record.RecipientId, record.Id, cancellationToken);
        await daprClient.SaveStateAsync(StoreName, dedupKey, true, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetByRecipientAsync(
        string tenantId, string recipientId,
        bool unreadOnly = false, string? type = null, int pageSize = 50,
        string? channel = null,
        CancellationToken cancellationToken = default)
    {
        var ids = await daprClient.GetStateAsync<List<Guid>>(StoreName, IndexKey(tenantId, recipientId), cancellationToken: cancellationToken) ?? [];
        var records = new List<NotificationRecord>(ids.Count);
        foreach (var id in ids)
        {
            var r = await daprClient.GetStateAsync<NotificationRecord>(StoreName, RecordKey(tenantId, recipientId, id), cancellationToken: cancellationToken);
            if (r is not null) records.Add(r);
        }

        var filtered = records.AsEnumerable();
        if (unreadOnly) filtered = filtered.Where(r => !r.IsRead);
        if (!string.IsNullOrEmpty(type))
            filtered = filtered.Where(r => r.NotificationType.StartsWith(type + ".", StringComparison.OrdinalIgnoreCase)
                                        || r.NotificationType.Equals(type, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(channel))
            filtered = filtered.Where(r => r.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));

        return filtered.OrderByDescending(r => r.CreatedAt).Take(pageSize).ToList();
    }

    public async Task<int> GetUnreadCountAsync(string tenantId, string recipientId, CancellationToken cancellationToken = default)
    {
        var ids = await daprClient.GetStateAsync<List<Guid>>(StoreName, IndexKey(tenantId, recipientId), cancellationToken: cancellationToken) ?? [];
        int count = 0;
        foreach (var id in ids)
        {
            var r = await daprClient.GetStateAsync<NotificationRecord>(StoreName, RecordKey(tenantId, recipientId, id), cancellationToken: cancellationToken);
            if (r is not null && !r.IsRead) count++;
        }
        return count;
    }

    public async Task<bool> MarkReadAsync(Guid notificationId, string tenantId, string recipientId, CancellationToken cancellationToken = default)
    {
        var key = RecordKey(tenantId, recipientId, notificationId);
        var record = await daprClient.GetStateAsync<NotificationRecord>(StoreName, key, cancellationToken: cancellationToken);
        if (record is null) return false;
        record.MarkRead();
        await daprClient.SaveStateAsync(StoreName, key, record, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<int> DeleteByRecipientIdAsync(string tenantId, string recipientId, CancellationToken cancellationToken = default)
    {
        var indexKey = IndexKey(tenantId, recipientId);
        var ids = await daprClient.GetStateAsync<List<Guid>>(StoreName, indexKey, cancellationToken: cancellationToken) ?? [];
        foreach (var id in ids)
            await daprClient.DeleteStateAsync(StoreName, RecordKey(tenantId, recipientId, id), cancellationToken: cancellationToken);
        if (ids.Count > 0)
            await daprClient.DeleteStateAsync(StoreName, indexKey, cancellationToken: cancellationToken);
        return ids.Count;
    }

    private async Task AddToIndexAsync(string tenantId, string recipientId, Guid notificationId, CancellationToken ct)
    {
        var key = IndexKey(tenantId, recipientId);
        var index = await daprClient.GetStateAsync<List<Guid>>(StoreName, key, cancellationToken: ct) ?? [];
        if (!index.Contains(notificationId))
        {
            index.Add(notificationId);
            await daprClient.SaveStateAsync(StoreName, key, index, cancellationToken: ct);
        }
    }

    private static string RecordKey(string tenantId, string recipientId, Guid notificationId)
        => TenantStorageKey.For("notification", tenantId, $"{recipientId}:{notificationId}");
    private static string IndexKey(string tenantId, string recipientId)
        => TenantStorageKey.For("notif-index", tenantId, recipientId);
    private static string DedupKey(string tenantId, string deduplicationKey)
        => TenantStorageKey.For("notif-dedup", tenantId, deduplicationKey);
}
