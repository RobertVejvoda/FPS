using FPS.Notification.Application;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Infrastructure;

/// <summary>
/// NOTIF #728 — resolves an employee notification recipient (a user ID) to a verified Profile
/// <c>NotificationAddress</c> via <see cref="IProfileRecipientLookup"/>. It never trusts an
/// event/caller-supplied address: an email-shaped recipient ID is still resolved through Profile
/// (and fails closed if no matching verified profile exists), so a corrupt Booking event cannot
/// redirect employee mail. Sales/onboarding alerts do not use this resolver — their configured
/// address is passed straight to transport by their handler. Never logs the recipient ID or address;
/// any lookup failure fails closed so the caller records a delivery-rejected outcome and skips SendGrid.
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

        try
        {
            // Always resolve through Profile — an email-shaped recipient ID is NOT trusted directly.
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
}
