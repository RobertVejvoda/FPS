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
    public Task<EmailSendResult> SendAsync(
        NotificationRecord record, string recipientEmail, ComposedEmail email, CancellationToken cancellationToken = default)
    {
        // Logs the composed plain-text body (not the HTML) so the local/test path exercises and
        // surfaces the text alternative the composer produces. The resolved address is not logged.
        logger.LogInformation(
            "[Email-stub] Type={Type} Subject={Subject} Text={Text}",
            record.NotificationType, email.Subject, email.TextBody);

        return Task.FromResult(EmailSendResult.Ok());
    }
}
