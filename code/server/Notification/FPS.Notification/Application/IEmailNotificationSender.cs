using FPS.Notification.Domain;

namespace FPS.Notification.Application;

public interface IEmailNotificationSender
{
    // NOTIF #727 — transport receives already-composed content; the record carries recipient and
    // routing/logging context only. Senders must not build subject/body themselves.
    Task<EmailSendResult> SendAsync(
        NotificationRecord record, ComposedEmail email, CancellationToken cancellationToken = default);
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
