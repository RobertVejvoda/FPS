namespace FPS.Notification.Application;

/// <summary>
/// AUTH008B (#734) — transient delivery of a FairSpot-local email-verification message. This is NOT an
/// in-app notification: no durable <c>NotificationRecord</c> is created and the verification link (which
/// carries the Secret token) is never persisted or logged. The link exists only in memory and in the
/// provider send request. Kept separate from the notification-record sender precisely so the link never
/// reaches a body-logging path.
/// </summary>
public interface IVerificationEmailDelivery
{
    Task<bool> SendAsync(VerificationEmailRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The verification link already embeds the one-time token (Secret) — treat the whole value as Secret.</summary>
public sealed record VerificationEmailRequest(string TenantId, string EmailAddress, string VerificationLink);
