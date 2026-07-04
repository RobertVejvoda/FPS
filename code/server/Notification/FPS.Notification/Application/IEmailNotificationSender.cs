using FPS.Notification.Domain;

namespace FPS.Notification.Application;

public interface IEmailNotificationSender
{
    // NOTIF #727 — transport receives already-composed content; the record carries routing/logging
    // context only. Senders must not build subject/body themselves.
    // NOTIF #728 — the destination is the already-resolved, verified recipientEmail (the record's
    // RecipientId is a user ID for employee events, never the address).
    Task<EmailSendResult> SendAsync(
        NotificationRecord record, string recipientEmail, ComposedEmail email, CancellationToken cancellationToken = default);
}

public sealed record EmailSendResult(bool Success, string? FailureReason, string? FailureCategory = null)
{
    public static EmailSendResult Ok() => new(true, null);
    public static EmailSendResult Fail(string reason, string? category = null) => new(false, reason, category);
}

public static class EmailFailureCategory
{
    public const string ProviderUnavailable = "provider_unavailable";
    public const string DeliveryRejected = "delivery_rejected";
}
