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
    public string SourceEventId { get; init; } = string.Empty;
    public string DeliveryStatus { get; private set; } = NotificationDeliveryStatus.Stored;
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; init; }

    public void MarkRead() => IsRead = true;
}

public static class NotificationChannel
{
    public const string InApp = "in-app";
    public const string Email = "email";
}

public static class NotificationDeliveryStatus
{
    public const string Stored = "stored";
    public const string Failed = "failed";
}
