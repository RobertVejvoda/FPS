using FPS.Notification.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Notification.Tests.Infrastructure;

public sealed class NotificationTenantPurgeTests
{
    // ── Write-path: recipient index ───────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_WritesTenantRecipientIndex()
    {
        var harness = new NotificationStoreHarness();
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("demo", "user-1"));

        Assert.True(harness.Store.TryGetValue("notif-recipients:demo:all", out var value));
        var recipients = Assert.IsType<List<string>>(value);
        Assert.Equal(["user-1"], recipients);
    }

    [Fact]
    public async Task SaveAsync_RecipientIndex_IsIdempotentAndAccumulates()
    {
        var harness = new NotificationStoreHarness();
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("demo", "user-1"));
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("demo", "user-1"));
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("demo", "user-2"));

        var recipients = Assert.IsType<List<string>>(harness.Store["notif-recipients:demo:all"]);
        Assert.Equal(2, recipients.Count);
        Assert.Contains("user-1", recipients);
        Assert.Contains("user-2", recipients);
    }

    [Fact]
    public async Task PreferencesSaveAsync_AlsoWritesRecipientIndex()
    {
        var harness = new NotificationStoreHarness();
        await harness.Preferences.SaveAsync(NotificationPreferences.Default("demo", "prefs-user"));

        var recipients = Assert.IsType<List<string>>(harness.Store["notif-recipients:demo:all"]);
        Assert.Contains("prefs-user", recipients);
    }

    // ── Purge ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PurgeAsync_RemovesAllTenantData_AndReturnsTotal()
    {
        var harness = new NotificationStoreHarness();

        // 2 recipients: user-1 has two notifications, user-2 has one.
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("ten-1", "user-1"));
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("ten-1", "user-1"));
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("ten-1", "user-2"));
        await harness.Preferences.SaveAsync(NotificationPreferences.Default("ten-1", "user-1"));
        await harness.Preferences.SaveAsync(NotificationPreferences.Default("ten-1", "user-2"));
        harness.Roster.Set("ten-1", ["hr-1", "hr-2"]);

        var count = await harness.Purger.PurgeAsync(TenantPurgeScope.For("ten-1"), sandboxReset: true, CancellationToken.None);

        // 3 notifications + 2 preferences + 1 roster = 6.
        Assert.Equal(6, count);
        Assert.Empty(await harness.Notifications.GetByRecipientAsync("ten-1", "user-1"));
        Assert.Empty(await harness.Notifications.GetByRecipientAsync("ten-1", "user-2"));
        Assert.False(harness.Store.ContainsKey("notif-recipients:ten-1:all"));
        Assert.False(harness.Store.ContainsKey("notif-roster:ten-1:all"));
        Assert.DoesNotContain(harness.Store.Keys, k => k.StartsWith("notif-prefs:ten-1:", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Store.Keys, k => k.StartsWith("notification:ten-1:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PurgeAsync_IsIdempotent_SecondCallReturnsZero()
    {
        var harness = new NotificationStoreHarness();
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("ten-1", "user-1"));
        await harness.Preferences.SaveAsync(NotificationPreferences.Default("ten-1", "user-1"));
        harness.Roster.Set("ten-1", ["hr-1"]);

        var first = await harness.Purger.PurgeAsync(TenantPurgeScope.For("ten-1"), true, CancellationToken.None);
        var second = await harness.Purger.PurgeAsync(TenantPurgeScope.For("ten-1"), true, CancellationToken.None);

        Assert.True(first > 0);
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task PurgeAsync_LeavesOtherTenantsUntouched()
    {
        var harness = new NotificationStoreHarness();
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("ten-1", "user-1"));
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("ten-2", "user-1"));

        await harness.Purger.PurgeAsync(TenantPurgeScope.For("ten-1"), true, CancellationToken.None);

        Assert.Empty(await harness.Notifications.GetByRecipientAsync("ten-1", "user-1"));
        Assert.Single(await harness.Notifications.GetByRecipientAsync("ten-2", "user-1"));
    }

    [Fact]
    public async Task PurgeAsync_LeavesDedupMarkersAsSafeResidue()
    {
        var harness = new NotificationStoreHarness();
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("ten-1", "user-1"));

        await harness.Purger.PurgeAsync(TenantPurgeScope.For("ten-1"), true, CancellationToken.None);

        // dedup markers are not enumerable and are intentionally left (documented safe residue).
        Assert.Contains(harness.Store.Keys, k => k.StartsWith("notif-dedup:ten-1:", StringComparison.Ordinal));
    }
}
