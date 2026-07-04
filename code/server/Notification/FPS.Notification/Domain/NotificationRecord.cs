namespace FPS.Notification.Domain;

public sealed class NotificationRecord
{
    public Guid Id { get; init; }
    public string DeduplicationKey { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string RecipientId { get; init; } = string.Empty;
    public string NotificationType { get; init; } = string.Empty;
    public string Channel { get; init; } = NotificationChannel.InApp;
    public string MessageText { get; init; } = string.Empty;
    public string? RelatedRequestId { get; init; }
    public string? RelatedDate { get; init; }
    public string? RelatedTimeSlot { get; init; }
    public string? LocationId { get; init; }
    public string? NextAction { get; init; }
    // NOTIF #727 — business-safe outcome differentiators the email composer uses to pick a distinct
    // template for variants that share a NotificationType (reallocation, allocated-reservation
    // cancellation, late-cancel vs no-show penalty). Safe category values only — never internals.
    public string? AllocationSource { get; init; }
    public string? ReasonCode { get; init; }
    public string? PreviousStatus { get; init; }
    public string SourceEventId { get; init; } = string.Empty;
    public string DeliveryStatus { get; private set; } = NotificationDeliveryStatus.Stored;
    public string? FailureReason { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; init; }

    public void MarkRead() => IsRead = true;
    public void MarkDelivered() => DeliveryStatus = NotificationDeliveryStatus.Sent;
    public void MarkFailed(string reason)
    {
        DeliveryStatus = NotificationDeliveryStatus.Failed;
        FailureReason = reason;
    }
}

public static class NotificationChannel
{
    public const string InApp = "in-app";
    public const string Email = "email";
}

public static class NotificationDeliveryStatus
{
    public const string Stored = "stored";
    public const string Sent = "sent";
    public const string Failed = "failed";
}
