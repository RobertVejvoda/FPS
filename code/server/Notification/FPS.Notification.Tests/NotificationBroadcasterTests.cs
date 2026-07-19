using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;

namespace FPS.Notification.Tests;

public sealed class NotificationBroadcasterTests
{
    private readonly InMemoryNotificationBroadcaster broadcaster = new();

    private static NotificationRecord MakeRecord(string tenantId, string recipientId) => new()
    {
        Id = Guid.NewGuid(),
        DeduplicationKey = Guid.NewGuid().ToString(),
        TenantId = tenantId,
        RecipientId = recipientId,
        NotificationType = "booking.requestSubmitted",
        Channel = NotificationChannel.InApp,
        MessageText = "Test",
        SourceEventId = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Broadcast_DeliverToMatchingSubscriber()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var record = MakeRecord("t1", "u1");

        var received = new List<NotificationRecord>();
        var subscribing = Task.Run(async () =>
        {
            await foreach (var n in broadcaster.SubscribeAsync("t1", "u1", cts.Token))
                received.Add(n);
        });

        // Poll until the subscription is registered, then until delivery arrives.
        // SubscribeAsync registers synchronously on first MoveNextAsync, so a fixed
        // delay before broadcasting races the thread pool under load. (Matches the
        // pattern already used by Broadcast_DeliverToMultipleMatchingSubscribers.)
        while (broadcaster.SubscriptionCount < 1 && !cts.Token.IsCancellationRequested)
            await Task.Delay(5);

        await broadcaster.BroadcastAsync(record);

        while (received.Count == 0 && !cts.Token.IsCancellationRequested)
            await Task.Delay(5);

        cts.Cancel();
        await subscribing.ContinueWith(_ => { }); // ignore cancellation exception

        Assert.Single(received);
        Assert.Equal(record.Id, received[0].Id);
    }

    [Fact]
    public async Task Broadcast_DoesNotDeliverToOtherTenant()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var received = new List<NotificationRecord>();
        var subscribing = Task.Run(async () =>
        {
            await foreach (var n in broadcaster.SubscribeAsync("t1", "u1", cts.Token))
                received.Add(n);
        });

        // Wait until the subscription is registered so the test genuinely exercises
        // the tenant filter (not a not-yet-subscribed no-op), then broadcast a
        // non-matching record and give any erroneous delivery time to arrive.
        while (broadcaster.SubscriptionCount < 1 && !cts.Token.IsCancellationRequested)
            await Task.Delay(5);

        await broadcaster.BroadcastAsync(MakeRecord("t2", "u1")); // different tenant
        await Task.Delay(50);

        cts.Cancel();
        await subscribing.ContinueWith(_ => { });

        Assert.Empty(received);
    }

    [Fact]
    public async Task Broadcast_DoesNotDeliverToOtherRecipient()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var received = new List<NotificationRecord>();
        var subscribing = Task.Run(async () =>
        {
            await foreach (var n in broadcaster.SubscribeAsync("t1", "u1", cts.Token))
                received.Add(n);
        });

        // Wait until the subscription is registered so the test genuinely exercises
        // the recipient filter, then broadcast a non-matching record and give any
        // erroneous delivery time to arrive.
        while (broadcaster.SubscriptionCount < 1 && !cts.Token.IsCancellationRequested)
            await Task.Delay(5);

        await broadcaster.BroadcastAsync(MakeRecord("t1", "u2")); // different recipient
        await Task.Delay(50);

        cts.Cancel();
        await subscribing.ContinueWith(_ => { });

        Assert.Empty(received);
    }

    [Fact]
    public async Task Broadcast_DeliverToMultipleMatchingSubscribers()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var record = MakeRecord("t1", "u1");

        var received1 = new List<NotificationRecord>();
        var received2 = new List<NotificationRecord>();

        var sub1 = Task.Run(async () =>
        {
            await foreach (var n in broadcaster.SubscribeAsync("t1", "u1", cts.Token))
                received1.Add(n);
        });
        var sub2 = Task.Run(async () =>
        {
            await foreach (var n in broadcaster.SubscribeAsync("t1", "u1", cts.Token))
                received2.Add(n);
        });

        // Poll until both subscriptions are registered; SubscribeAsync registers
        // synchronously (no await before TryAdd), so once SubscriptionCount == 2
        // both channels are ready to receive — no fixed-delay race condition.
        while (broadcaster.SubscriptionCount < 2 && !cts.Token.IsCancellationRequested)
            await Task.Delay(5);

        await broadcaster.BroadcastAsync(record);

        // Poll for delivery instead of a fixed delay.
        while ((received1.Count == 0 || received2.Count == 0) && !cts.Token.IsCancellationRequested)
            await Task.Delay(5);

        cts.Cancel();
        await Task.WhenAll(
            sub1.ContinueWith(_ => { }),
            sub2.ContinueWith(_ => { }));

        Assert.Single(received1);
        Assert.Single(received2);
    }
}
