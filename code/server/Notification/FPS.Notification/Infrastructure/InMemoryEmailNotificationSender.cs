using FPS.Notification.Application;
using FPS.Notification.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Infrastructure;

// CONTRACT GAP: BookingEventPayload supplies only recipientId (a user ID), not an email address.
// A real implementation requires either:
//   (a) an email address field added to the Booking event payload, or
//   (b) a Profile/Identity lookup at delivery time (out of scope for N003).
// This sender logs the delivery for local/test use and treats it as success.
// Replace with a Dapr output binding adapter (e.g. SendGrid/SMTP component) for production.
public sealed class InMemoryEmailNotificationSender(ILogger<InMemoryEmailNotificationSender> logger)
    : IEmailNotificationSender
{
    public Task<EmailSendResult> SendAsync(NotificationRecord record, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[Email-stub] To={RecipientId} Type={Type} Message={Message}",
            record.RecipientId, record.NotificationType, record.MessageText);

        return Task.FromResult(EmailSendResult.Ok());
    }
}
