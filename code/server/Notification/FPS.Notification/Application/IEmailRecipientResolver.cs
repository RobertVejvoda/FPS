namespace FPS.Notification.Application;

/// <summary>
/// NOTIF #728 — resolves a notification recipient (a user ID for employee events, or an already-configured
/// email for sales/onboarding alerts) to a verified delivery email address. Employee events never carry
/// email addresses; resolution goes through trusted Profile/Identity data. Fails closed: an unresolved,
/// unverified, missing, malformed, or ambiguous recipient yields a rejection with a safe reason and the
/// caller must not attempt a provider send.
/// </summary>
public interface IEmailRecipientResolver
{
    Task<ResolvedRecipient> ResolveAsync(string tenantId, string recipientId, CancellationToken cancellationToken = default);
}

public sealed record ResolvedRecipient(bool Resolved, string? Email, string? RejectionReason)
{
    public static ResolvedRecipient Ok(string email) => new(true, email, null);
    public static ResolvedRecipient Reject(string reason) => new(false, null, reason);
}
