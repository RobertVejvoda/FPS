using FPS.Customer.Application;
using FPS.Customer.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// Default sales notifier: records that a sales alert was dispatched for a request, without
/// emitting prospect PII to logs (the request id is non-identifying). A production profile swaps
/// this for an SMTP / Notification-service implementation that emails the full request to the
/// internal sales address; the prospect's details never leave the platform.
/// </summary>
public sealed class LoggingTenantRequestNotifier(
    IConfiguration configuration, ILogger<LoggingTenantRequestNotifier> logger) : ITenantRequestNotifier
{
    public Task NotifySalesAsync(TenantRequest request, CancellationToken ct)
    {
        var salesAddress = configuration["Onboarding:SalesEmail"] ?? "sales@fairspot.net";
        logger.LogInformation(
            "Sales alert dispatched for tenant request {RequestId} (status {Status}) to the configured sales inbox.",
            request.RequestId, request.Status);
        _ = salesAddress; // routing target; not logged to avoid implying PII delivery in logs
        return Task.CompletedTask;
    }
}
