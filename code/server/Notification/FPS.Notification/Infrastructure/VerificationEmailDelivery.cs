using System.Net;
using System.Net.Mail;
using System.Text;
using FPS.Notification.Application;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Infrastructure;

/// <summary>
/// AUTH008B (#734) — composes the customer-ready verification email. The link (Secret) is only ever
/// placed in the returned HTML/text that goes straight to the provider send; nothing here logs it.
/// </summary>
internal static class VerificationEmailContent
{
    public const string Subject = "Verify your FairSpot email address";

    public static string Html(string verificationLink)
    {
        var link = WebUtility.HtmlEncode(verificationLink);
        var sb = new StringBuilder();
        sb.Append("<div style=\"margin:0;padding:0;background-color:#f4f5f7;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:#f4f5f7;\"><tr><td align=\"center\" style=\"padding:24px 12px;\">");
        sb.Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;width:100%;background-color:#ffffff;border-radius:8px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;\">");
        sb.Append("<tr><td style=\"background-color:#1f2937;padding:16px 24px;\"><span style=\"color:#ffffff;font-size:18px;font-weight:bold;letter-spacing:0.5px;\">FairSpot</span></td></tr>");
        sb.Append("<tr><td style=\"padding:24px 24px 8px 24px;\"><h1 style=\"margin:0;font-size:20px;color:#111827;\">Confirm your email address</h1></td></tr>");
        sb.Append("<tr><td style=\"padding:8px 24px 0 24px;\"><p style=\"margin:0;font-size:15px;line-height:1.5;color:#374151;\">Please confirm this email address belongs to you so FairSpot can send you parking notifications. This link expires shortly and can be used once.</p></td></tr>");
        sb.Append("<tr><td style=\"padding:20px 24px 4px 24px;\">");
        sb.Append($"<a href=\"{link}\" style=\"display:inline-block;background-color:#1f2937;color:#ffffff;text-decoration:none;font-size:15px;font-weight:bold;padding:12px 20px;border-radius:6px;\">Verify my email</a>");
        sb.Append("</td></tr>");
        sb.Append($"<tr><td style=\"padding:12px 24px 0 24px;\"><p style=\"margin:0;font-size:12px;line-height:1.5;color:#6b7280;\">If the button does not work, copy this link into your browser:<br>{link}</p></td></tr>");
        sb.Append("<tr><td style=\"padding:24px;\"><hr style=\"border:none;border-top:1px solid #e5e7eb;margin:0 0 12px 0;\"><p style=\"margin:0;font-size:12px;line-height:1.5;color:#9ca3af;\">If you did not request this, you can safely ignore this email — no changes will be made.</p></td></tr>");
        sb.Append("</table></td></tr></table></div>");
        return sb.ToString();
    }

    public static string Text(string verificationLink) =>
        "FairSpot\nConfirm your email address\n\n" +
        "Please confirm this email address belongs to you so FairSpot can send you parking notifications. " +
        "This link expires shortly and can be used once:\n\n" +
        verificationLink + "\n\n—\nIf you did not request this, you can safely ignore this email.\n";
}

/// <summary>
/// Real transport: sends the verification email through the shared SendGrid transport. NOTIF #731 — the
/// verification email now goes out multipart/alternative (both the HTML and plain-text parts from
/// <see cref="VerificationEmailContent"/>). The composed body carries the Secret link but is sent
/// transiently: no record is persisted and the link is never logged.
/// </summary>
public sealed class DaprBindingVerificationEmailDelivery(
    ISendGridEmailTransport transport,
    ILogger<DaprBindingVerificationEmailDelivery> logger) : IVerificationEmailDelivery
{
    public async Task<bool> SendAsync(VerificationEmailRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeEmail(request.EmailAddress, out var recipient))
        {
            logger.LogWarning("Verification email not sent: recipient address invalid. TenantId={TenantId}", request.TenantId);
            return false;
        }

        var sent = await transport.SendAsync(
            new SendGridEmailMessage(
                recipient,
                null,
                VerificationEmailContent.Subject,
                VerificationEmailContent.Html(request.VerificationLink),
                VerificationEmailContent.Text(request.VerificationLink)),
            cancellationToken);

        if (!sent)
        {
            // No link/address in the log. The Secret token in the link is never logged.
            logger.LogWarning("Verification email delivery failed (provider unavailable). TenantId={TenantId}", request.TenantId);
        }
        return sent;
    }

    private static bool TryNormalizeEmail(string candidate, out string address)
    {
        address = string.Empty;
        var trimmed = candidate.Trim();
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace) || !trimmed.Contains('@', StringComparison.Ordinal))
            return false;
        try
        {
            var parsed = new MailAddress(trimmed);
            if (!string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase)) return false;
            address = parsed.Address;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>
/// Local/dev transport: records that a verification email was sent WITHOUT the link, token, or address —
/// so the local path never logs the Secret. Used when no SendGrid provider is configured.
/// </summary>
public sealed class LogSafeVerificationEmailDelivery(ILogger<LogSafeVerificationEmailDelivery> logger) : IVerificationEmailDelivery
{
    public Task<bool> SendAsync(VerificationEmailRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[Verification-email-stub] sent for TenantId={TenantId} (link not logged).", request.TenantId);
        return Task.FromResult(true);
    }
}
