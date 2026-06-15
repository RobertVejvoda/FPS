using FPS.Notification.Application;

namespace FPS.Notification.Infrastructure;

public sealed class RosterBackedAudienceResolver(IHrRosterStore rosterStore) : INotificationAudienceResolver
{
    public Task<IReadOnlyCollection<string>> GetHrRecipientsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tenantId))
            return Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());

        return Task.FromResult(rosterStore.GetHrUserIds(tenantId));
    }
}
