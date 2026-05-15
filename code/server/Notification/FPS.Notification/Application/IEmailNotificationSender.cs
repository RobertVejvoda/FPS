using FPS.Notification.Domain;

namespace FPS.Notification.Application;

public interface IEmailNotificationSender
{
    Task<EmailSendResult> SendAsync(NotificationRecord record, CancellationToken cancellationToken = default);
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
