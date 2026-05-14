using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;

namespace FPS.Notification.Tests;

public sealed class NotificationRepositoryTests
{
    private readonly InMemoryNotificationRepository repo = new();

    private static NotificationRecord MakeRecord(string tenantId, string recipientId,
        string notificationType = "booking.requestSubmitted",
        bool isRead = false,
        DateTime? createdAt = null)
    {
        var record = new NotificationRecord
        {
            Id = Guid.NewGuid(),
            DeduplicationKey = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            RecipientId = recipientId,
            NotificationType = notificationType,
            Channel = NotificationChannel.InApp,
            MessageText = "Test message",
            SourceEventId = Guid.NewGuid().ToString(),
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        if (isRead) record.MarkRead();
        return record;
    }

    [Fact]
    public async Task GetByRecipient_ReturnsOnlyMatchingTenantAndRecipient()
    {
        await repo.SaveAsync(MakeRecord("tenant-1", "user-1"));
        await repo.SaveAsync(MakeRecord("tenant-1", "user-2"));
        await repo.SaveAsync(MakeRecord("tenant-2", "user-1"));

        var results = await repo.GetByRecipientAsync("tenant-1", "user-1");

        Assert.Single(results);
        Assert.Equal("user-1", results[0].RecipientId);
        Assert.Equal("tenant-1", results[0].TenantId);
    }

    [Fact]
    public async Task GetByRecipient_ReturnsNewestFirst()
    {
        var older = MakeRecord("t1", "u1", createdAt: DateTime.UtcNow.AddMinutes(-5));
        var newer = MakeRecord("t1", "u1", createdAt: DateTime.UtcNow);
        await repo.SaveAsync(older);
        await repo.SaveAsync(newer);

        var results = await repo.GetByRecipientAsync("t1", "u1");

        Assert.Equal(newer.Id, results[0].Id);
        Assert.Equal(older.Id, results[1].Id);
    }

    [Fact]
    public async Task GetByRecipient_UnreadOnly_ExcludesReadNotifications()
    {
        var read = MakeRecord("t1", "u1", isRead: true);
        var unread = MakeRecord("t1", "u1");
        await repo.SaveAsync(read);
        await repo.SaveAsync(unread);

        var results = await repo.GetByRecipientAsync("t1", "u1", unreadOnly: true);

        Assert.Single(results);
        Assert.Equal(unread.Id, results[0].Id);
    }

    [Fact]
    public async Task GetByRecipient_TypeFilter_MatchesByPrefix()
    {
        await repo.SaveAsync(MakeRecord("t1", "u1", notificationType: "booking.slotAllocated"));
        await repo.SaveAsync(MakeRecord("t1", "u1", notificationType: "penalty.applied"));

        var results = await repo.GetByRecipientAsync("t1", "u1", type: "booking");

        Assert.Single(results);
        Assert.Equal("booking.slotAllocated", results[0].NotificationType);
    }

    [Fact]
    public async Task GetByRecipient_ExcludesInternalFields_NotPresentOnDto()
    {
        await repo.SaveAsync(MakeRecord("t1", "u1"));

        var result = (await repo.GetByRecipientAsync("t1", "u1")).Single();

        // Internal fields exist on the record but must never be surfaced on NotificationDto
        Assert.NotEmpty(result.DeduplicationKey);
        Assert.NotEmpty(result.SourceEventId);
        // The controller maps to NotificationDto which omits these — verified by type not having the fields
        var dtoProps = typeof(FPS.Notification.Controllers.NotificationDto)
            .GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("DeduplicationKey", dtoProps);
        Assert.DoesNotContain("SourceEventId", dtoProps);
        Assert.DoesNotContain("DeliveryStatus", dtoProps);
    }

    [Fact]
    public async Task GetByRecipient_PageSize_LimitsResults()
    {
        for (int i = 0; i < 10; i++)
            await repo.SaveAsync(MakeRecord("t1", "u1"));

        var results = await repo.GetByRecipientAsync("t1", "u1", pageSize: 3);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task GetUnreadCount_CountsOnlyUnreadForUserAndTenant()
    {
        await repo.SaveAsync(MakeRecord("t1", "u1", isRead: false));
        await repo.SaveAsync(MakeRecord("t1", "u1", isRead: false));
        await repo.SaveAsync(MakeRecord("t1", "u1", isRead: true));
        await repo.SaveAsync(MakeRecord("t1", "u2", isRead: false));
        await repo.SaveAsync(MakeRecord("t2", "u1", isRead: false));

        int count = await repo.GetUnreadCountAsync("t1", "u1");

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task MarkRead_MarksOnlyAuthenticatedUserNotification()
    {
        var target = MakeRecord("t1", "u1");
        var other = MakeRecord("t1", "u2");
        await repo.SaveAsync(target);
        await repo.SaveAsync(other);

        bool found = await repo.MarkReadAsync(target.Id, "t1", "u1");

        Assert.True(found);
        Assert.True(target.IsRead);
        Assert.False(other.IsRead);
    }

    [Fact]
    public async Task MarkRead_ReturnsFalse_WhenNotificationBelongsToOtherUser()
    {
        var record = MakeRecord("t1", "u1");
        await repo.SaveAsync(record);

        bool found = await repo.MarkReadAsync(record.Id, "t1", "u2");

        Assert.False(found);
        Assert.False(record.IsRead);
    }

    [Fact]
    public async Task MarkRead_ReturnsFalse_WhenNotificationBelongsToOtherTenant()
    {
        var record = MakeRecord("t1", "u1");
        await repo.SaveAsync(record);

        bool found = await repo.MarkReadAsync(record.Id, "t2", "u1");

        Assert.False(found);
        Assert.False(record.IsRead);
    }

    [Fact]
    public async Task MarkRead_AlreadyRead_ReturnsTrueAndRemainsRead()
    {
        var record = MakeRecord("t1", "u1", isRead: true);
        await repo.SaveAsync(record);

        bool found = await repo.MarkReadAsync(record.Id, "t1", "u1");

        Assert.True(found);
        Assert.True(record.IsRead);
    }
}
