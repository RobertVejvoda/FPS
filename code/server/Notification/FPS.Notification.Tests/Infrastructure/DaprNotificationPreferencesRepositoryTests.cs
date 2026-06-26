using Dapr.Client;
using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

public sealed class DaprNotificationPreferencesRepositoryTests
{
    private const string StoreName = "notificationstore";
    private readonly Dictionary<string, object?> store = new();

    private DaprNotificationPreferencesRepository BuildRepo()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<NotificationPreferences>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, NotificationPreferences, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<NotificationPreferences>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as NotificationPreferences : null);

        return new DaprNotificationPreferencesRepository(mock.Object);
    }

    // ── Restart persistence ───────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenRestart_PreferencesSurvive()
    {
        var repo1 = BuildRepo();
        var prefs = NotificationPreferences.Default("demo", "user-1");
        prefs.Update(remindersEnabled: false, informationalEnabled: true, preferredReminderTiming: "morning");
        await repo1.SaveAsync(prefs);

        var repo2 = BuildRepo();
        var loaded = await repo2.GetOrDefaultAsync("demo", "user-1");
        Assert.False(loaded.RemindersEnabled);
        Assert.Equal("morning", loaded.PreferredReminderTiming);
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_TenantIsolated_DoesNotLeakAcrossTenants()
    {
        var repo = BuildRepo();
        var prefs = NotificationPreferences.Default("tenant-a", "user-1");
        prefs.Update(false, false, null);
        await repo.SaveAsync(prefs);

        var other = await repo.GetOrDefaultAsync("tenant-b", "user-1");
        Assert.True(other.RemindersEnabled);
        Assert.True(other.InformationalEnabled);
    }

    // ── Default fallback ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrDefaultAsync_ReturnsDefault_WhenNotPersisted()
    {
        var repo = BuildRepo();
        var prefs = await repo.GetOrDefaultAsync("demo", "unknown-user");

        Assert.True(prefs.RemindersEnabled);
        Assert.True(prefs.InformationalEnabled);
        Assert.Null(prefs.PreferredReminderTiming);
    }

    // ── Update round-trip ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_OverwritesExistingPreferences()
    {
        var repo = BuildRepo();
        var prefs = NotificationPreferences.Default("demo", "user-1");
        prefs.Update(false, false, "evening");
        await repo.SaveAsync(prefs);

        prefs.Update(true, true, null);
        await repo.SaveAsync(prefs);

        var loaded = await repo.GetOrDefaultAsync("demo", "user-1");
        Assert.True(loaded.RemindersEnabled);
        Assert.Null(loaded.PreferredReminderTiming);
    }
}
