namespace FPS.Notification.Application;

// Per-tenant HR roster used by INotificationAudienceResolver. Kept as a
// separate seam from the resolver so the registry layer (currently
// in-memory, fed from configuration or future identity sync) can evolve
// independently of how the handler consumes it.
public interface IHrRosterStore
{
    IReadOnlyCollection<string> GetHrUserIds(string tenantId);

    void Set(string tenantId, IEnumerable<string> hrUserIds);
}
