namespace FPS.Notification.Application;

/// <summary>
/// PLAT004b — the tenant-request alert published by Customer on the <c>tenant-request-received</c>
/// topic. Matches the publisher's shape (pub/sub is by JSON contract). Carries only non-sensitive
/// routing fields; prospect contact PII never travels on the event.
/// </summary>
public sealed record TenantRequestEvent(
    string RequestId,
    string Company,
    string PrimaryDomain,
    DateTimeOffset OccurredAt);
