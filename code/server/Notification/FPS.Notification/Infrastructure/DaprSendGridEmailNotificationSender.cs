using System.Net.Mail;
using FPS.Notification.Application;
using FPS.Notification.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FPS.Notification.Infrastructure;

public sealed class DaprSendGridEmailOptions
{
    public const string SectionName = "Notification:Email";
    public string Provider { get; init; } = "InMemory";
    // Retained for the (now superseded) Dapr SendGrid binding component; the real send path is the
    // multipart HTTP transport (NOTIF #731).
    public string BindingName { get; init; } = "notification-email";
    public string SubjectPrefix { get; init; } = "FairSpot";
    public string? FromEmail { get; init; }
    public string? FromName { get; init; } = "FairSpot";
    // NOTIF #731 — the SendGrid API key is read from the Dapr secret store, not configuration/env.
    public string SecretStoreName { get; init; } = "secretstore";
    public string ApiKeySecretName { get; init; } = "sendgrid-credentials";
    public string ApiKeySecretKey { get; init; } = "apiKey";
}

/// <summary>
/// SendGrid email sender for normal notification emails. NOTIF #731 — sends both the composed HTML and
/// plain-text bodies as a multipart/alternative message through <see cref="ISendGridEmailTransport"/>
/// (the Dapr binding could only send HTML). Transport-only: subject/body arrive pre-composed (#727) and the
/// destination is the already-resolved verified address (#728).
/// </summary>
public sealed class DaprSendGridEmailNotificationSender(
    ISendGridEmailTransport transport,
    ILogger<DaprSendGridEmailNotificationSender> logger) : IEmailNotificationSender
{
    private static readonly HashSet<string> EnabledProviderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SendGrid",
        "DaprSendGrid",
        "DaprBinding"
    };

    public static bool IsConfiguredProvider(string? provider) =>
        !string.IsNullOrWhiteSpace(provider) && EnabledProviderNames.Contains(provider.Trim());

    public async Task<EmailSendResult> SendAsync(
        NotificationRecord record, string recipientEmail, ComposedEmail email, CancellationToken cancellationToken = default)
    {
        // NOTIF #728 — the destination is the already-resolved verified address; still validated here
        // as a defensive guard (the record's RecipientId is a user ID for employee events).
        if (!TryNormalizeEmailAddress(recipientEmail, out var recipientAddress))
        {
            logger.LogWarning(
                "Email delivery skipped because recipient address is unavailable. TenantId={TenantId} NotificationType={NotificationType} SourceEventId={SourceEventId} Channel={Channel}",
                record.TenantId, record.NotificationType, record.SourceEventId, record.Channel);

            return EmailSendResult.Fail(
                "Email recipient address unavailable",
                EmailFailureCategory.DeliveryRejected);
        }

        // NOTIF #731 — both HTML and plain-text parts are delivered (multipart/alternative).
        var sent = await transport.SendAsync(
            new SendGridEmailMessage(recipientAddress, null, email.Subject, email.HtmlBody, email.TextBody),
            cancellationToken);

        return sent
            ? EmailSendResult.Ok()
            : EmailSendResult.Fail("Email delivery unavailable", EmailFailureCategory.ProviderUnavailable);
    }

    private static bool TryNormalizeEmailAddress(string recipientId, out string address)
    {
        address = string.Empty;
        var trimmed = recipientId.Trim();
        if (trimmed.Length == 0 ||
            !trimmed.Contains('@', StringComparison.Ordinal) ||
            trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(trimmed);
            if (!string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            address = parsed.Address;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
