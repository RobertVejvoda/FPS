using System.Collections.Concurrent;
using FPS.Notification.Application;
using FPS.Notification.Domain;

namespace FPS.Notification.Infrastructure;

public sealed class InMemoryNotificationPreferencesRepository : INotificationPreferencesRepository
{
    private readonly ConcurrentDictionary<(string, string), NotificationPreferences> _store = new();

    public Task<NotificationPreferences> GetOrDefaultAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var prefs = _store.TryGetValue((tenantId, userId), out var existing)
            ? existing
            : NotificationPreferences.Default(tenantId, userId);
        return Task.FromResult(prefs);
    }

    public Task SaveAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default)
    {
        _store[(preferences.TenantId, preferences.UserId)] = preferences;
        return Task.CompletedTask;
    }
}
