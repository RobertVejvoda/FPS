using FPS.Customer.Application;
using FPS.Customer.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// Default record-only sales notifier: it notes that a tenant request awaits a sales alert but
/// does <b>not</b> send email. Real delivery (Customer → Notification → <c>sales@fairspot.net</c>)
/// ships in PLAT004b (#651), which swaps in a pub/sub implementation of this seam. Prospect PII is
/// never written to logs (the request id is non-identifying).
/// </summary>
public sealed class LoggingTenantRequestNotifier(
    ILogger<LoggingTenantRequestNotifier> logger) : ITenantRequestNotifier
{
    public Task NotifySalesAsync(TenantRequest request, CancellationToken ct)
    {
        logger.LogInformation(
            "Tenant request {RequestId} recorded; sales-email delivery is not yet wired (PLAT004b #651) — no email sent.",
            request.RequestId);
        return Task.CompletedTask;
    }
}
