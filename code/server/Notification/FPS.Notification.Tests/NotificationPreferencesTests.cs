using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;

namespace FPS.Notification.Tests;

public sealed class NotificationPreferencesTests
{
    private readonly InMemoryNotificationPreferencesRepository _repo = new();

    // ── defaults ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrDefault_ReturnsDefaults_WhenNoneStored()
    {
        var prefs = await _repo.GetOrDefaultAsync("tenant-1", "user-1");

        Assert.Equal("tenant-1", prefs.TenantId);
        Assert.Equal("user-1", prefs.UserId);
        Assert.True(prefs.RemindersEnabled);
        Assert.True(prefs.InformationalEnabled);
        Assert.Null(prefs.PreferredReminderTiming);
    }

    // ── tenant isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task Preferences_AreScopedByTenantAndUser()
    {
        var p1 = NotificationPreferences.Default("tenant-1", "user-1");
        p1.Update(remindersEnabled: false, informationalEnabled: true, null);
        await _repo.SaveAsync(p1);

        var p2 = await _repo.GetOrDefaultAsync("tenant-2", "user-1");
        var p3 = await _repo.GetOrDefaultAsync("tenant-1", "user-2");

        Assert.True(p2.RemindersEnabled, "Different tenant must not share preferences");
        Assert.True(p3.RemindersEnabled, "Different user must not share preferences");
    }

    // ── update roundtrip ────────────────────────────────────────────────────

    [Fact]
    public async Task Update_PersistsAllFields()
    {
        var prefs = await _repo.GetOrDefaultAsync("t1", "u1");
        prefs.Update(remindersEnabled: false, informationalEnabled: false, "1day");
        await _repo.SaveAsync(prefs);

        var loaded = await _repo.GetOrDefaultAsync("t1", "u1");

        Assert.False(loaded.RemindersEnabled);
        Assert.False(loaded.InformationalEnabled);
        Assert.Equal("1day", loaded.PreferredReminderTiming);
    }

    [Fact]
    public async Task Update_ClearsReminderTiming_WhenNullOrWhitespace()
    {
        var prefs = await _repo.GetOrDefaultAsync("t1", "u1");
        prefs.Update(true, true, "1day");
        await _repo.SaveAsync(prefs);

        prefs.Update(true, true, "  ");
        await _repo.SaveAsync(prefs);

        var loaded = await _repo.GetOrDefaultAsync("t1", "u1");
        Assert.Null(loaded.PreferredReminderTiming);
    }

    // ── mandatory notification protection ───────────────────────────────────

    [Fact]
    public void AllowsDelivery_CriticalOperational_Always_True()
    {
        var prefs = NotificationPreferences.Default("t1", "u1");
        prefs.Update(remindersEnabled: false, informationalEnabled: false, null);

        Assert.True(prefs.AllowsDelivery(NotificationClass.CriticalOperational),
            "Critical operational notifications must never be suppressed by user preference");
    }

    [Fact]
    public void AllowsDelivery_Reminder_RespectsPreference()
    {
        var prefs = NotificationPreferences.Default("t1", "u1");

        prefs.Update(remindersEnabled: true, informationalEnabled: true, null);
        Assert.True(prefs.AllowsDelivery(NotificationClass.Reminder));

        prefs.Update(remindersEnabled: false, informationalEnabled: true, null);
        Assert.False(prefs.AllowsDelivery(NotificationClass.Reminder));
    }

    [Fact]
    public void AllowsDelivery_Informational_RespectsPreference()
    {
        var prefs = NotificationPreferences.Default("t1", "u1");

        prefs.Update(remindersEnabled: true, informationalEnabled: true, null);
        Assert.True(prefs.AllowsDelivery(NotificationClass.Informational));

        prefs.Update(remindersEnabled: true, informationalEnabled: false, null);
        Assert.False(prefs.AllowsDelivery(NotificationClass.Informational));
    }

    // ── classifier ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("booking.requestSubmitted")]
    [InlineData("booking.slotAllocated")]
    [InlineData("booking.requestRejected")]
    [InlineData("booking.penaltyApplied")]
    [InlineData("booking.noShowRecorded")]
    [InlineData("booking.manualCorrectionApplied")]
    [InlineData("booking.drawCompleted")]
    [InlineData("booking.requestCancelled")]
    [InlineData("booking.usageConfirmed")]
    [InlineData("booking.requestExpired")]
    public void Classifier_BookingEvents_AreCriticalOperational(string notificationType)
    {
        Assert.Equal(NotificationClass.CriticalOperational,
            NotificationClassifier.Classify(notificationType));
    }

    [Fact]
    public void Classifier_UnknownType_DefaultsToCriticalOperational()
    {
        Assert.Equal(NotificationClass.CriticalOperational,
            NotificationClassifier.Classify("unknown.event.type"));
    }
}
