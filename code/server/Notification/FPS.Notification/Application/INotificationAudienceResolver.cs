namespace FPS.Notification.Application;

// Resolves the broader business audience for an event beyond the requestor.
// Today: HR managers per tenant. Tenant-admin and other audiences can be
// added later without changing the handler contract.
//
// The handler stays publisher-agnostic — it does not need to know which
// users hold HR roles or how that mapping is materialised. The resolver
// is the single seam where role-based fan-out is decided, which is why
// the InMemory backing store is registered in Program.cs but can be
// swapped for a Dapr-backed store once tenant identity propagation
// reaches Notification.
public interface INotificationAudienceResolver
{
    Task<IReadOnlyCollection<string>> GetHrRecipientsAsync(string tenantId, CancellationToken cancellationToken = default);
}
