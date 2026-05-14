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
        using var cts = new CancellationTokenSource();
        var record = MakeRecord("t1", "u1");

        var received = new List<NotificationRecord>();
        var subscribing = Task.Run(async () =>
        {
            await foreach (var n in broadcaster.SubscribeAsync("t1", "u1", cts.Token))
                received.Add(n);
        });

        await Task.Delay(50); // let subscription register
        await broadcaster.BroadcastAsync(record);
        await Task.Delay(50); // let delivery complete

        cts.Cancel();
        await subscribing.ContinueWith(_ => { }); // ignore cancellation exception

        Assert.Single(received);
        Assert.Equal(record.Id, received[0].Id);
    }

    [Fact]
    public async Task Broadcast_DoesNotDeliverToOtherTenant()
    {
        using var cts = new CancellationTokenSource();

        var received = new List<NotificationRecord>();
        var subscribing = Task.Run(async () =>
        {
            await foreach (var n in broadcaster.SubscribeAsync("t1", "u1", cts.Token))
                received.Add(n);
        });

        await Task.Delay(50);
        await broadcaster.BroadcastAsync(MakeRecord("t2", "u1")); // different tenant
        await Task.Delay(50);

        cts.Cancel();
        await subscribing.ContinueWith(_ => { });

        Assert.Empty(received);
    }

    [Fact]
    public async Task Broadcast_DoesNotDeliverToOtherRecipient()
    {
        using var cts = new CancellationTokenSource();

        var received = new List<NotificationRecord>();
        var subscribing = Task.Run(async () =>
        {
            await foreach (var n in broadcaster.SubscribeAsync("t1", "u1", cts.Token))
                received.Add(n);
        });

        await Task.Delay(50);
        await broadcaster.BroadcastAsync(MakeRecord("t1", "u2")); // different recipient
        await Task.Delay(50);

        cts.Cancel();
        await subscribing.ContinueWith(_ => { });

        Assert.Empty(received);
    }

    [Fact]
    public async Task Broadcast_DeliverToMultipleMatchingSubscribers()
    {
        using var cts = new CancellationTokenSource();
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

        await Task.Delay(200); // two concurrent subscribers need more time to register on CI
        await broadcaster.BroadcastAsync(record);
        await Task.Delay(200);

        cts.Cancel();
        await Task.WhenAll(
            sub1.ContinueWith(_ => { }),
            sub2.ContinueWith(_ => { }));

        Assert.Single(received1);
        Assert.Single(received2);
    }
}
