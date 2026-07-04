using System.Net.Mail;
using FPS.Notification.Application;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Infrastructure;

/// <summary>
/// NOTIF #728 — resolves recipients to verified delivery addresses. Sales/onboarding alerts whose
/// recipient is already a configured email address are trusted as-is; employee user IDs are resolved
/// to a verified Profile <c>NotificationAddress</c> via <see cref="IProfileRecipientLookup"/>. Never
/// logs the recipient ID or email address; any lookup failure fails closed so the caller records a
/// delivery-rejected outcome and does not call SendGrid.
/// </summary>
public sealed class DaprEmailRecipientResolver(
    IProfileRecipientLookup profileLookup,
    ILogger<DaprEmailRecipientResolver> logger) : IEmailRecipientResolver
{
    public async Task<ResolvedRecipient> ResolveAsync(
        string tenantId, string recipientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
            return ResolvedRecipient.Reject("recipient_missing");

        // Sales/onboarding alerts carry an already-configured email address — trust it directly rather
        // than resolving through Profile.
        if (IsWellFormedEmail(recipientId))
            return ResolvedRecipient.Ok(recipientId.Trim());

        try
        {
            var result = await profileLookup.LookupAsync(tenantId, recipientId, cancellationToken);
            if (result is { Resolved: true } && !string.IsNullOrWhiteSpace(result.Email))
                return ResolvedRecipient.Ok(result.Email!);

            return ResolvedRecipient.Reject(result?.Reason ?? "email_unresolved");
        }
        catch (Exception)
        {
            // No recipient ID or address in the log — privacy rule. Fail closed.
            logger.LogWarning(
                "Recipient email resolution unavailable for tenant {TenantId}; failing closed.", tenantId);
            return ResolvedRecipient.Reject("recipient_resolution_unavailable");
        }
    }

    private static bool IsWellFormedEmail(string candidate)
    {
        var trimmed = candidate.Trim();
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace) || !trimmed.Contains('@', StringComparison.Ordinal))
            return false;
        try
        {
            var parsed = new MailAddress(trimmed);
            return string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
