using FPS.Notification.Application;
using FPS.Notification.Domain;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace FPS.Notification.Infrastructure;

public sealed class InMemoryNotificationBroadcaster : INotificationBroadcaster
{
    private sealed record Subscription(string TenantId, string RecipientId, Channel<NotificationRecord> Channel);

    private readonly ConcurrentDictionary<Guid, Subscription> subscriptions = new();

    public Task BroadcastAsync(NotificationRecord record, CancellationToken cancellationToken = default)
    {
        foreach (var sub in subscriptions.Values)
        {
            if (sub.TenantId == record.TenantId && sub.RecipientId == record.RecipientId)
                sub.Channel.Writer.TryWrite(record);
        }
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<NotificationRecord> SubscribeAsync(
        string tenantId, string recipientId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<NotificationRecord>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        var id = Guid.NewGuid();
        subscriptions.TryAdd(id, new Subscription(tenantId, recipientId, channel));
        try
        {
            await foreach (var record in channel.Reader.ReadAllAsync(cancellationToken))
                yield return record;
        }
        finally
        {
            subscriptions.TryRemove(id, out _);
            channel.Writer.TryComplete();
        }
    }
}
