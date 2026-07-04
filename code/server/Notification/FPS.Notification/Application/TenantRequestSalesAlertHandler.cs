using FPS.Notification.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Application;

/// <summary>
/// PLAT004b — emails the FairSpot sales inbox when a prospect requests a tenant. The alert carries
/// only company + domain + request id and directs sales to the authenticated operator queue for
/// full details; the prospect's contact email and message are never placed in the alert (so they
/// are not delivered to third parties or written to delivery logs).
///
/// Dapr pub/sub is at-least-once, so this follows the Notification dedup contract
/// (docs/business-layer/notification.md): a stable deduplication key per source event, an
/// ExistsAsync gate so a replayed event does not re-send, and a persisted delivery-status record
/// (delivered / failed) so support can see outcomes. Delivery failures are persisted, not thrown —
/// the request is already durably recorded and visible in the operator queue.
/// </summary>
public sealed class TenantRequestSalesAlertHandler(
    INotificationRepository repository,
    IEmailNotificationSender emailSender,
    IEmailNotificationComposer emailComposer,
    IConfiguration configuration,
    ILogger<TenantRequestSalesAlertHandler> logger)
{
    public const string DefaultSalesAddress = "sales@fairspot.net";
    public const string NotificationType = "tenant-request.received";
    private const string PlatformTenant = "platform";

    public async Task HandleAsync(TenantRequestEvent @event, CancellationToken ct = default)
    {
        var dedupKey = $"tenant-request:{@event.RequestId}";
        if (await repository.ExistsAsync(dedupKey, PlatformTenant, ct))
            return; // already handled this source event — at-least-once replay must not re-send

        var salesAddress = configuration["Onboarding:SalesEmail"];
        if (string.IsNullOrWhiteSpace(salesAddress)) salesAddress = DefaultSalesAddress;

        var record = new NotificationRecord
        {
            Id = Guid.NewGuid(),
            DeduplicationKey = dedupKey,
            TenantId = PlatformTenant,
            RecipientId = salesAddress,
            NotificationType = NotificationType,
            Channel = NotificationChannel.Email,
            MessageText =
                $"New tenant request: {@event.Company} ({@event.PrimaryDomain}). " +
                $"Review and triage in the platform operator queue — request {@event.RequestId}.",
            SourceEventId = @event.RequestId,
            CreatedAt = DateTime.UtcNow,
        };

        var composed = emailComposer.Compose(record);
        EmailSendResult result;
        try
        {
            // NOTIF #728 — the sales/onboarding recipient is an already-configured email address, so it
            // is passed straight to transport (no Profile recipient resolution needed).
            result = await emailSender.SendAsync(record, salesAddress, composed, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sales alert delivery threw for tenant request {RequestId}.", @event.RequestId);
            result = EmailSendResult.Fail("Email delivery unavailable", EmailFailureCategory.ProviderUnavailable);
        }

        if (result.Success)
        {
            record.MarkDelivered();
            logger.LogInformation("Sales alert emailed for tenant request {RequestId}.", @event.RequestId);
        }
        else
        {
            record.MarkFailed(result.FailureReason ?? "Unknown error");
            logger.LogWarning("Sales alert for tenant request {RequestId} was not delivered: {Reason}.",
                @event.RequestId, result.FailureReason);
        }

        await repository.SaveAsync(record, ct);
    }
}
