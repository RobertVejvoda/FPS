namespace FPS.Notification.Application;

/// <summary>
/// NOTIF #728 — resolves an <b>employee</b> notification recipient user ID to a verified delivery email
/// address through trusted Profile/Identity data. Employee events never carry email addresses, and an
/// event/caller-supplied address is never trusted. Fails closed: an unresolved, unverified, missing,
/// malformed, or ambiguous recipient yields a rejection with a safe reason and the caller must not
/// attempt a provider send. (Sales/onboarding alerts do not use this resolver — their configured
/// address is passed straight to transport.)
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
