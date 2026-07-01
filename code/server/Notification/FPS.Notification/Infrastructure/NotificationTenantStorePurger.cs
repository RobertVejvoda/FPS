using FPS.Notification.Application;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Notification.Infrastructure;

/// <summary>
/// <see cref="ITenantStorePurger"/> for the notification bounded context (PLAT003C). Deletes all of a
/// tenant's <c>notificationstore</c> data: notification records and their per-recipient indexes,
/// notification preferences, the HR roster (and its registry entry), and the per-tenant recipient
/// index. Notification data is operational, not immutable evidence, so it is removed on a normal
/// single-tenant purge as well as on a sandbox reset.
/// </summary>
public sealed class NotificationTenantStorePurger(
    INotificationRepository notifications,
    INotificationPreferencesRepository preferences,
    DaprHrRosterStore roster) : ITenantStorePurger
{
    public string Service => "notification";

    public bool IsImmutableEvidence => false;

    public async Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var tenantId = scope.TenantId;

        // Ordering matters: the preferences purge reads the shared notif-recipients index that the
        // notification purge then deletes, so preferences must run first. Roster is independent.
        var preferencesRemoved = await preferences.PurgeTenantAsync(tenantId, ct);
        var notificationsRemoved = await notifications.PurgeTenantAsync(tenantId, ct);
        var rosterRemoved = await roster.PurgeTenantAsync(tenantId, ct);

        return preferencesRemoved + notificationsRemoved + rosterRemoved;
    }
}
