using System.Net;
using System.Net.Mail;
using System.Text;
using Dapr.Client;
using FPS.Notification.Application;
using FPS.Notification.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FPS.Notification.Infrastructure;

public sealed class DaprSendGridEmailOptions
{
    public const string SectionName = "Notification:Email";
    public string Provider { get; init; } = "InMemory";
    public string BindingName { get; init; } = "notification-email";
    public string SubjectPrefix { get; init; } = "FairSpot";
    public string? FromEmail { get; init; }
    public string? FromName { get; init; } = "FairSpot";
}

public sealed class DaprSendGridEmailNotificationSender(
    DaprClient daprClient,
    IOptions<DaprSendGridEmailOptions> options,
    ILogger<DaprSendGridEmailNotificationSender> logger) : IEmailNotificationSender
{
    private const string CreateOperation = "create";
    private static readonly HashSet<string> EnabledProviderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SendGrid",
        "DaprSendGrid",
        "DaprBinding"
    };

    public static bool IsConfiguredProvider(string? provider) =>
        !string.IsNullOrWhiteSpace(provider) && EnabledProviderNames.Contains(provider.Trim());

    public async Task<EmailSendResult> SendAsync(
        NotificationRecord record, ComposedEmail email, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeEmailAddress(record.RecipientId, out var recipientAddress))
        {
            logger.LogWarning(
                "Email delivery skipped because recipient address is unavailable. TenantId={TenantId} NotificationType={NotificationType} SourceEventId={SourceEventId} Channel={Channel}",
                record.TenantId, record.NotificationType, record.SourceEventId, record.Channel);

            return EmailSendResult.Fail(
                "Email recipient address unavailable",
                EmailFailureCategory.DeliveryRejected);
        }

        var configured = options.Value;
        // NOTIF #727 — transport only: subject/body come pre-composed. The upstream Dapr
        // `bindings.twilio.sendgrid` binding sends a single `text/html` content part, so this path
        // delivers email.HtmlBody only. email.TextBody is still composed (used by the in-memory/logging
        // sender and ready for a multipart-capable transport) but is NOT delivered here today; true
        // multipart HTML+text delivery is tracked as a follow-up (see #727 review).
        var request = new BindingRequest(BindingName(configured), CreateOperation)
        {
            Data = Encoding.UTF8.GetBytes(email.HtmlBody)
        };
        request.Metadata["emailTo"] = recipientAddress;
        request.Metadata["subject"] = email.Subject;

        if (!string.IsNullOrWhiteSpace(configured.FromEmail))
        {
            request.Metadata["emailFrom"] = configured.FromEmail.Trim();
        }

        if (!string.IsNullOrWhiteSpace(configured.FromName))
        {
            request.Metadata["emailFromName"] = configured.FromName.Trim();
        }

        try
        {
            await daprClient.InvokeBindingAsync(request, cancellationToken);
            return EmailSendResult.Ok();
        }
        catch (Exception)
        {
            return EmailSendResult.Fail(
                "Email delivery unavailable",
                EmailFailureCategory.ProviderUnavailable);
        }
    }

    private static string BindingName(DaprSendGridEmailOptions options) =>
        string.IsNullOrWhiteSpace(options.BindingName)
            ? "notification-email"
            : options.BindingName.Trim();

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
