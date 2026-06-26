using Dapr.Client;
using FPS.Notification.Application;
using FPS.Notification.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Notification.Infrastructure;

public sealed class DaprNotificationPreferencesRepository : INotificationPreferencesRepository
{
    private readonly DaprClient daprClient;
    private const string StoreName = "notificationstore";

    public DaprNotificationPreferencesRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<NotificationPreferences> GetOrDefaultAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var prefs = await daprClient.GetStateAsync<NotificationPreferences>(StoreName, PrefsKey(tenantId, userId), cancellationToken: cancellationToken);
        return prefs ?? NotificationPreferences.Default(tenantId, userId);
    }

    public async Task SaveAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default)
        => await daprClient.SaveStateAsync(StoreName, PrefsKey(preferences.TenantId, preferences.UserId), preferences, cancellationToken: cancellationToken);

    private static string PrefsKey(string tenantId, string userId)
        => TenantStorageKey.For("notif-prefs", tenantId, userId);
}
