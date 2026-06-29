namespace FPS.Customer.Application;

/// <summary>
/// PLAT004b — published to the Notification pub/sub when a tenant request is submitted, so sales is
/// alerted. Deliberately carries only non-sensitive routing fields (company, domain, id): the
/// prospect's contact email and message stay in the durable store and are seen only through the
/// authenticated operator queue, never in an event payload, an email body, or a log line.
/// </summary>
public sealed record TenantRequestEvent(
    string RequestId,
    string Company,
    string PrimaryDomain,
    DateTimeOffset OccurredAt);
