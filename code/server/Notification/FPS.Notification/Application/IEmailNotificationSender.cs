using FPS.Notification.Domain;

namespace FPS.Notification.Application;

public interface IEmailNotificationSender
{
    Task<EmailSendResult> SendAsync(NotificationRecord record, CancellationToken cancellationToken = default);
}

public sealed record EmailSendResult(bool Success, string? FailureReason)
{
    public static EmailSendResult Ok() => new(true, null);
    public static EmailSendResult Fail(string reason) => new(false, reason);
}
