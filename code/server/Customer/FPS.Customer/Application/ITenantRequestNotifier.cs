using FPS.Customer.Domain;

namespace FPS.Customer.Application;

/// <summary>
/// Alerts the FairSpot sales team that a prospect has requested a tenant. The implementation
/// must keep prospect PII inside the platform (no third-party logs/links); the message body
/// summary must not leak the address book or secrets.
/// </summary>
public interface ITenantRequestNotifier
{
    Task NotifySalesAsync(TenantRequest request, CancellationToken ct);
}
