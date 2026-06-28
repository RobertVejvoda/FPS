using FPS.Notification.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Application;

/// <summary>
/// PLAT004b — emails the FairSpot sales inbox when a prospect requests a tenant. The alert carries
/// only company + domain + request id and directs sales to the authenticated operator queue for
/// full details; the prospect's contact email and message are never placed in the alert (so they
/// are not delivered to third parties or written to delivery logs). Delivery failures are logged
/// and swallowed — the request is already durably recorded and visible in the operator queue.
/// </summary>
public sealed class TenantRequestSalesAlertHandler(
    IEmailNotificationSender emailSender,
    IConfiguration configuration,
    ILogger<TenantRequestSalesAlertHandler> logger)
{
    public const string DefaultSalesAddress = "sales@fairspot.net";
    public const string NotificationType = "tenant-request.received";

    public async Task HandleAsync(TenantRequestEvent @event, CancellationToken ct = default)
    {
        var salesAddress = configuration["Onboarding:SalesEmail"];
        if (string.IsNullOrWhiteSpace(salesAddress))
            salesAddress = DefaultSalesAddress;

        var record = new NotificationRecord
        {
            Id = Guid.NewGuid(),
            DeduplicationKey = $"tenant-request:{@event.RequestId}",
            TenantId = "platform",
            RecipientId = salesAddress,
            NotificationType = NotificationType,
            Channel = NotificationChannel.Email,
            MessageText =
                $"New tenant request: {@event.Company} ({@event.PrimaryDomain}). " +
                $"Review and triage in the platform operator queue — request {@event.RequestId}.",
            SourceEventId = @event.RequestId,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            var result = await emailSender.SendAsync(record, ct);
            if (result.Success)
                logger.LogInformation("Sales alert emailed for tenant request {RequestId}.", @event.RequestId);
            else
                logger.LogWarning("Sales alert for tenant request {RequestId} was not delivered: {Reason}.",
                    @event.RequestId, result.FailureReason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sales alert delivery threw for tenant request {RequestId}.", @event.RequestId);
        }
    }
}
