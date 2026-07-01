using Dapr.Client;
using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

/// <summary>
/// Shared Moq <see cref="DaprClient"/> harness backing all three notificationstore repositories with
/// one in-memory dictionary, so a purge can be exercised end-to-end (records, preferences, roster,
/// and the recipient index all live in the same simulated state store).
/// </summary>
internal sealed class NotificationStoreHarness
{
    private const string StoreName = "notificationstore";

    public Dictionary<string, object?> Store { get; } = new();
    public DaprNotificationRepository Notifications { get; }
    public DaprNotificationPreferencesRepository Preferences { get; }
    public DaprHrRosterStore Roster { get; }
    public NotificationTenantStorePurger Purger { get; }

    public NotificationStoreHarness()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<NotificationRecord>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, NotificationRecord, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => Store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<NotificationPreferences>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, NotificationPreferences, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => Store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<bool>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => Store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<List<Guid>>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<Guid>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => Store[key] = new List<Guid>(value))
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<List<string>>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<string>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => Store[key] = new List<string>(value))
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<NotificationRecord>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                Store.TryGetValue(key, out var v) ? v as NotificationRecord : null);

        mock.Setup(c => c.GetStateAsync<NotificationPreferences>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                Store.TryGetValue(key, out var v) ? v as NotificationPreferences : null);

        mock.Setup(c => c.GetStateAsync<bool>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                Store.TryGetValue(key, out var v) && v is bool b && b);

        mock.Setup(c => c.GetStateAsync<List<Guid>>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                Store.TryGetValue(key, out var v) ? v as List<Guid> : null);

        mock.Setup(c => c.GetStateAsync<List<string>>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                Store.TryGetValue(key, out var v) ? v as List<string> : null);

        mock.Setup(c => c.DeleteStateAsync(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, _, _, _) => Store.Remove(key))
            .Returns(Task.CompletedTask);

        Notifications = new DaprNotificationRepository(mock.Object);
        Preferences = new DaprNotificationPreferencesRepository(mock.Object);
        Roster = new DaprHrRosterStore(mock.Object, NullLogger<DaprHrRosterStore>.Instance);
        Purger = new NotificationTenantStorePurger(Notifications, Preferences, Roster);
    }

    public static NotificationRecord MakeRecord(string tenantId, string recipientId)
        => new()
        {
            Id = Guid.NewGuid(),
            DeduplicationKey = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            RecipientId = recipientId,
            NotificationType = "booking.requestSubmitted",
            Channel = NotificationChannel.InApp,
            MessageText = "Test",
            SourceEventId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
        };
}
