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
    {
        await daprClient.SaveStateAsync(StoreName, PrefsKey(preferences.TenantId, preferences.UserId), preferences, cancellationToken: cancellationToken);
        // A user configuring their notification preferences is a notification recipient. Record
        // them in the shared per-tenant recipient index so the tenant purge (PLAT003C) can enumerate
        // and delete these preferences — notif-prefs keys are not otherwise enumerable in Dapr KV.
        await NotificationRecipientIndex.AddAsync(daprClient, StoreName, preferences.TenantId, preferences.UserId, cancellationToken);
    }

    public async Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        // DESTRUCTIVE single-tenant purge (PLAT003C). Reads the shared recipient index (populated by
        // both SaveAsync here and by DaprNotificationRepository) and deletes each recipient's
        // preferences. Reads the index but does NOT delete it — DaprNotificationRepository.PurgeTenantAsync
        // owns that deletion, so this must run before it (the purger enforces the ordering). Idempotent.
        var recipients = await NotificationRecipientIndex.ReadAsync(daprClient, StoreName, tenantId, cancellationToken);
        var removed = 0;
        foreach (var userId in recipients)
        {
            var key = PrefsKey(tenantId, userId);
            var existing = await daprClient.GetStateAsync<NotificationPreferences>(StoreName, key, cancellationToken: cancellationToken);
            if (existing is not null)
            {
                await daprClient.DeleteStateAsync(StoreName, key, cancellationToken: cancellationToken);
                removed++;
            }
        }
        return removed;
    }

    private static string PrefsKey(string tenantId, string userId)
        => TenantStorageKey.For("notif-prefs", tenantId, userId);
}
