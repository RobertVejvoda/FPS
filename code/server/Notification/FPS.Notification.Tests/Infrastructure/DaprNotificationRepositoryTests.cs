using Dapr.Client;
using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

public sealed class DaprNotificationRepositoryTests
{
    private const string StoreName = "notificationstore";
    private readonly Dictionary<string, object?> store = new();

    private DaprNotificationRepository BuildRepo()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<NotificationRecord>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, NotificationRecord, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<bool>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<List<Guid>>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<Guid>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<NotificationRecord>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as NotificationRecord : null);

        mock.Setup(c => c.GetStateAsync<bool>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) && v is bool b && b);

        mock.Setup(c => c.GetStateAsync<List<Guid>>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as List<Guid> : null);

        mock.Setup(c => c.DeleteStateAsync(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, _, _, _) => store.Remove(key))
            .Returns(Task.CompletedTask);

        return new DaprNotificationRepository(mock.Object);
    }

    private static NotificationRecord MakeRecord(
        string tenantId = "demo",
        string recipientId = "user-1",
        string notificationType = "booking.requestSubmitted",
        string channel = NotificationChannel.InApp,
        bool isRead = false,
        DateTime? createdAt = null,
        string? dedupKey = null)
    {
        var record = new NotificationRecord
        {
            Id = Guid.NewGuid(),
            DeduplicationKey = dedupKey ?? Guid.NewGuid().ToString(),
            TenantId = tenantId,
            RecipientId = recipientId,
            NotificationType = notificationType,
            Channel = channel,
            MessageText = "Test",
            SourceEventId = Guid.NewGuid().ToString(),
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
        if (isRead) record.MarkRead();
        return record;
    }

    // ── Restart persistence ───────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenRestart_RecordSurvives()
    {
        var repo1 = BuildRepo();
        var record = MakeRecord();
        await repo1.SaveAsync(record);

        var repo2 = BuildRepo();
        var results = await repo2.GetByRecipientAsync(record.TenantId, record.RecipientId);
        Assert.Single(results);
        Assert.Equal(record.Id, results[0].Id);
    }

    [Fact]
    public async Task SaveAsync_Duplicate_IdempotentAcrossRestart()
    {
        var repo1 = BuildRepo();
        var record = MakeRecord();
        await repo1.SaveAsync(record);

        var repo2 = BuildRepo();
        await repo2.SaveAsync(record);

        var results = await repo2.GetByRecipientAsync(record.TenantId, record.RecipientId);
        Assert.Single(results);
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_SameDedupKey_DifferentTenants_BothAccepted()
    {
        var repo = BuildRepo();
        var sharedDedupKey = Guid.NewGuid().ToString();
        var r1 = MakeRecord("tenant-a", dedupKey: sharedDedupKey);
        var r2 = MakeRecord("tenant-b", dedupKey: sharedDedupKey);
        await repo.SaveAsync(r1);
        await repo.SaveAsync(r2);

        var aResults = await repo.GetByRecipientAsync("tenant-a", "user-1");
        var bResults = await repo.GetByRecipientAsync("tenant-b", "user-1");
        Assert.Single(aResults);
        Assert.Single(bResults);
    }

    [Fact]
    public async Task ExistsAsync_SameDedupKey_DifferentTenants_ReturnsFalseForOtherTenant()
    {
        var repo = BuildRepo();
        var record = MakeRecord("tenant-a");
        await repo.SaveAsync(record);

        Assert.True(await repo.ExistsAsync(record.DeduplicationKey, "tenant-a"));
        Assert.False(await repo.ExistsAsync(record.DeduplicationKey, "tenant-b"));
    }

    [Fact]
    public async Task GetByRecipient_ReturnsOnlyMatchingTenant()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeRecord("tenant-1", "user-1"));
        await repo.SaveAsync(MakeRecord("tenant-2", "user-1"));

        var results = await repo.GetByRecipientAsync("tenant-1", "user-1");
        Assert.Single(results);
        Assert.Equal("tenant-1", results[0].TenantId);
    }

    // ── Read behaviour ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByRecipient_ReturnsNewestFirst()
    {
        var repo = BuildRepo();
        var older = MakeRecord(createdAt: DateTime.UtcNow.AddMinutes(-5));
        var newer = MakeRecord(createdAt: DateTime.UtcNow);
        await repo.SaveAsync(older);
        await repo.SaveAsync(newer);

        var results = await repo.GetByRecipientAsync("demo", "user-1");
        Assert.Equal(newer.Id, results[0].Id);
    }

    [Fact]
    public async Task GetByRecipient_UnreadOnlyFilter_Works()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeRecord(isRead: false));
        await repo.SaveAsync(MakeRecord(isRead: true));

        var results = await repo.GetByRecipientAsync("demo", "user-1", unreadOnly: true);
        Assert.Single(results);
        Assert.False(results[0].IsRead);
    }

    [Fact]
    public async Task GetByRecipient_TypeFilter_MatchesByPrefix()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeRecord(notificationType: "booking.slotAllocated"));
        await repo.SaveAsync(MakeRecord(notificationType: "penalty.applied"));

        var results = await repo.GetByRecipientAsync("demo", "user-1", type: "booking");
        Assert.Single(results);
        Assert.Equal("booking.slotAllocated", results[0].NotificationType);
    }

    [Fact]
    public async Task GetByRecipient_ChannelFilter_Works()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeRecord(channel: NotificationChannel.InApp));
        await repo.SaveAsync(MakeRecord(channel: NotificationChannel.Email));

        var results = await repo.GetByRecipientAsync("demo", "user-1", channel: NotificationChannel.InApp);
        Assert.Single(results);
        Assert.Equal(NotificationChannel.InApp, results[0].Channel);
    }

    [Fact]
    public async Task GetByRecipient_PageSize_LimitsResults()
    {
        var repo = BuildRepo();
        for (int i = 0; i < 5; i++)
            await repo.SaveAsync(MakeRecord());

        var results = await repo.GetByRecipientAsync("demo", "user-1", pageSize: 3);
        Assert.Equal(3, results.Count);
    }

    // ── Unread count ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUnreadCount_CountsCorrectly()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeRecord(isRead: false));
        await repo.SaveAsync(MakeRecord(isRead: false));
        await repo.SaveAsync(MakeRecord(isRead: true));

        var count = await repo.GetUnreadCountAsync("demo", "user-1");
        Assert.Equal(2, count);
    }

    // ── MarkRead ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkReadAsync_UpdatesPersistentState()
    {
        var repo1 = BuildRepo();
        var record = MakeRecord();
        await repo1.SaveAsync(record);

        await repo1.MarkReadAsync(record.Id, "demo", "user-1");

        var repo2 = BuildRepo();
        var results = await repo2.GetByRecipientAsync("demo", "user-1");
        Assert.True(results[0].IsRead);
    }

    [Fact]
    public async Task MarkReadAsync_ReturnsFalse_WhenNotFoundOrWrongTenant()
    {
        var repo = BuildRepo();
        var record = MakeRecord("tenant-1");
        await repo.SaveAsync(record);

        Assert.False(await repo.MarkReadAsync(record.Id, "tenant-2", "user-1"));
    }

    // ── Erasure (DeleteByRecipientId) ─────────────────────────────────────────

    [Fact]
    public async Task DeleteByRecipientId_RemovesAllRecordsAndIndex()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeRecord("ten-1", "user-1"));
        await repo.SaveAsync(MakeRecord("ten-1", "user-1"));
        await repo.SaveAsync(MakeRecord("ten-1", "user-2"));

        var count = await repo.DeleteByRecipientIdAsync("ten-1", "user-1");

        Assert.Equal(2, count);
        Assert.Empty(await repo.GetByRecipientAsync("ten-1", "user-1"));
        Assert.Single(await repo.GetByRecipientAsync("ten-1", "user-2"));
    }

    [Fact]
    public async Task DeleteByRecipientId_TenantIsolation_DoesNotAffectOtherTenant()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeRecord("ten-1", "user-1"));
        await repo.SaveAsync(MakeRecord("ten-2", "user-1"));

        await repo.DeleteByRecipientIdAsync("ten-1", "user-1");

        Assert.Single(await repo.GetByRecipientAsync("ten-2", "user-1"));
    }
}
