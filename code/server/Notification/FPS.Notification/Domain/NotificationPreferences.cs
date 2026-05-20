namespace FPS.Notification.Domain;

public sealed class NotificationPreferences
{
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public bool RemindersEnabled { get; private set; } = true;
    public bool InformationalEnabled { get; private set; } = true;
    public string? PreferredReminderTiming { get; private set; }

    public static NotificationPreferences Default(string tenantId, string userId) =>
        new() { TenantId = tenantId, UserId = userId };

    public void Update(bool remindersEnabled, bool informationalEnabled, string? preferredReminderTiming)
    {
        RemindersEnabled = remindersEnabled;
        InformationalEnabled = informationalEnabled;
        PreferredReminderTiming = string.IsNullOrWhiteSpace(preferredReminderTiming)
            ? null : preferredReminderTiming.Trim();
    }

    public bool AllowsDelivery(NotificationClass notificationClass) => notificationClass switch
    {
        NotificationClass.CriticalOperational => true,
        NotificationClass.Reminder => RemindersEnabled,
        NotificationClass.Informational => InformationalEnabled,
        _ => true,
    };
}

public enum NotificationClass
{
    CriticalOperational,
    Reminder,
    Informational,
}

public static class NotificationClassifier
{
    // Booking event types are all critical operational.
    // Reminder and Informational types are added in later slices.
    private static readonly HashSet<string> ReminderTypes = [];
    private static readonly HashSet<string> InformationalTypes = [];

    public static NotificationClass Classify(string notificationType) =>
        ReminderTypes.Contains(notificationType) ? NotificationClass.Reminder :
        InformationalTypes.Contains(notificationType) ? NotificationClass.Informational :
        NotificationClass.CriticalOperational;
}
