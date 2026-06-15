using System.Collections.Concurrent;
using FPS.Notification.Application;

namespace FPS.Notification.Infrastructure;

// Source of truth for HR-role user IDs per tenant while the Notification
// service does not yet receive identity events. Production wiring seeds
// this from configuration on startup; tests inject rosters directly.
// Replace with an event-fed roster once tenant identity propagates here.
public sealed class InMemoryHrRosterStore : IHrRosterStore
{
    private readonly ConcurrentDictionary<string, HashSet<string>> rosters = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> GetHrUserIds(string tenantId) =>
        rosters.TryGetValue(tenantId, out var users)
            ? users.ToArray()
            : Array.Empty<string>();

    public void Set(string tenantId, IEnumerable<string> hrUserIds)
    {
        var snapshot = new HashSet<string>(hrUserIds.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
        rosters[tenantId] = snapshot;
    }
}
