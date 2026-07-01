using Dapr.Client;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Notification.Infrastructure;

/// <summary>
/// Per-tenant index of the recipient (user) ids that have data in the <c>notificationstore</c>.
/// Dapr KV cannot enumerate keys by prefix, so a single-tenant purge (PLAT003C) needs an explicit
/// list of the recipients whose notification records and preferences must be deleted. The index is
/// appended idempotently on every persist of recipient-scoped data (a notification for a recipient,
/// or that recipient's notification preferences) and is read — then deleted — by the tenant purge.
/// Key: <c>notif-recipients:{tenantId}:all</c>.
/// </summary>
internal static class NotificationRecipientIndex
{
    internal static string Key(string tenantId)
        => TenantStorageKey.For("notif-recipients", tenantId, "all");

    /// <summary>Idempotently appends a recipient id to the tenant's recipient index.</summary>
    internal static async Task AddAsync(
        DaprClient daprClient, string storeName, string tenantId, string recipientId, CancellationToken ct)
    {
        var key = Key(tenantId);
        var recipients = await daprClient.GetStateAsync<List<string>>(storeName, key, cancellationToken: ct) ?? [];
        if (!recipients.Contains(recipientId, StringComparer.Ordinal))
        {
            recipients.Add(recipientId);
            await daprClient.SaveStateAsync(storeName, key, recipients, cancellationToken: ct);
        }
    }

    /// <summary>Returns the tenant's recipient ids (empty when the index is absent).</summary>
    internal static async Task<IReadOnlyList<string>> ReadAsync(
        DaprClient daprClient, string storeName, string tenantId, CancellationToken ct)
        => await daprClient.GetStateAsync<List<string>>(storeName, Key(tenantId), cancellationToken: ct) ?? [];

    internal static async Task DeleteAsync(
        DaprClient daprClient, string storeName, string tenantId, CancellationToken ct)
        => await daprClient.DeleteStateAsync(storeName, Key(tenantId), cancellationToken: ct);
}
