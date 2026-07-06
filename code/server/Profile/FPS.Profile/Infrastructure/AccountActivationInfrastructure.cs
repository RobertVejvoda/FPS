using System.Security.Cryptography;
using System.Text;
using Dapr.Client;
using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.SharedKernel.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FPS.Profile.Infrastructure;

/// <summary>
/// AUTH009 (#738) — persistence for pending-account activation. One record per (tenant, user) in the
/// profilestore (supersede-by-overwrite), plus a global challenge-id → (tenant, user) index so the
/// anonymous confirm path can resolve the record from the opaque challenge id alone. Distinct key prefix
/// from AUTH008B email-verification so the two token spaces never collide.
/// </summary>
public sealed class DaprAccountActivationRepository(DaprClient daprClient) : IAccountActivationRepository
{
    private const string StoreName = "profilestore";

    public Task<AccountActivation?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        daprClient.GetStateAsync<AccountActivation>(StoreName, Key(tenantId, userId), cancellationToken: cancellationToken);

    public async Task<(string TenantId, string UserId)?> ResolveChallengeAsync(
        string challengeId, CancellationToken cancellationToken = default)
    {
        var reference = await daprClient.GetStateAsync<ChallengeReference>(
            StoreName, ChallengeKey(challengeId), cancellationToken: cancellationToken);
        return reference is null ? null : (reference.TenantId, reference.UserId);
    }

    public async Task SaveAsync(AccountActivation activation, CancellationToken cancellationToken = default)
    {
        await daprClient.SaveStateAsync(
            StoreName, Key(activation.TenantId, activation.UserId), activation, cancellationToken: cancellationToken);
        await daprClient.SaveStateAsync(
            StoreName, ChallengeKey(activation.ChallengeId),
            new ChallengeReference(activation.TenantId, activation.UserId), cancellationToken: cancellationToken);
        await AddToTenantIndexAsync(activation.TenantId, activation.UserId, cancellationToken);
        await AddToTenantChallengeIndexAsync(activation.TenantId, activation.ChallengeId, cancellationToken);
    }

    public async Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var indexKey = TenantIndexKey(tenantId);
        var challengeIndexKey = TenantChallengeIndexKey(tenantId);
        var userIds = await daprClient.GetStateAsync<List<string>>(StoreName, indexKey, cancellationToken: cancellationToken) ?? [];
        var challengeIds = await daprClient.GetStateAsync<List<string>>(StoreName, challengeIndexKey, cancellationToken: cancellationToken) ?? [];

        var removed = 0;
        // Remove all challenge-id index entries ever issued for the tenant, including superseded links
        // that no longer appear on the current per-user record.
        foreach (var challengeId in challengeIds)
            await daprClient.DeleteStateAsync(StoreName, ChallengeKey(challengeId), cancellationToken: cancellationToken);

        foreach (var userId in userIds)
        {
            var key = Key(tenantId, userId);
            var record = await daprClient.GetStateAsync<AccountActivation>(StoreName, key, cancellationToken: cancellationToken);
            if (record is null)
                continue; // stale index entry — nothing to remove
            // Backward-compatible cleanup for records written before the tenant challenge index existed.
            await daprClient.DeleteStateAsync(StoreName, ChallengeKey(record.ChallengeId), cancellationToken: cancellationToken);
            await daprClient.DeleteStateAsync(StoreName, key, cancellationToken: cancellationToken);
            removed++;
        }

        await daprClient.DeleteStateAsync(StoreName, indexKey, cancellationToken: cancellationToken);
        await daprClient.DeleteStateAsync(StoreName, challengeIndexKey, cancellationToken: cancellationToken);
        return removed;
    }

    private async Task AddToTenantIndexAsync(string tenantId, string userId, CancellationToken cancellationToken)
    {
        var indexKey = TenantIndexKey(tenantId);
        var index = await daprClient.GetStateAsync<List<string>>(StoreName, indexKey, cancellationToken: cancellationToken) ?? [];
        if (!index.Contains(userId))
        {
            index.Add(userId);
            await daprClient.SaveStateAsync(StoreName, indexKey, index, cancellationToken: cancellationToken);
        }
    }

    private async Task AddToTenantChallengeIndexAsync(string tenantId, string challengeId, CancellationToken cancellationToken)
    {
        var indexKey = TenantChallengeIndexKey(tenantId);
        var index = await daprClient.GetStateAsync<List<string>>(StoreName, indexKey, cancellationToken: cancellationToken) ?? [];
        if (!index.Contains(challengeId))
        {
            index.Add(challengeId);
            await daprClient.SaveStateAsync(StoreName, indexKey, index, cancellationToken: cancellationToken);
        }
    }

    private static string Key(string tenantId, string userId) =>
        TenantStorageKey.For("account-activation", tenantId, userId);

    private static string TenantIndexKey(string tenantId) =>
        TenantStorageKey.For("account-activation-index", tenantId, "all");

    private static string TenantChallengeIndexKey(string tenantId) =>
        TenantStorageKey.For("account-activation-challenge-index", tenantId, "all");

    // Global (not tenant-scoped) index — the anonymous confirm path has no trusted tenant yet, and the
    // challenge id is high-entropy so enumeration is infeasible.
    private static string ChallengeKey(string challengeId) => $"account-activation-challenge:{challengeId}";

    private sealed record ChallengeReference(string TenantId, string UserId);
}

public sealed class InMemoryAccountActivationRepository : IAccountActivationRepository
{
    private readonly Dictionary<string, AccountActivation> store = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string TenantId, string UserId)> challenges = new(StringComparer.Ordinal);

    public Task<AccountActivation?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.GetValueOrDefault($"{tenantId}:{userId}"));

    public Task<(string TenantId, string UserId)?> ResolveChallengeAsync(string challengeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(challenges.TryGetValue(challengeId, out var reference) ? reference : ((string, string)?)null);

    public Task SaveAsync(AccountActivation activation, CancellationToken cancellationToken = default)
    {
        store[$"{activation.TenantId}:{activation.UserId}"] = activation;
        challenges[activation.ChallengeId] = (activation.TenantId, activation.UserId);
        return Task.CompletedTask;
    }

    public Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var keys = store.Where(kv => kv.Value.TenantId == tenantId).ToList();
        var challengeKeys = challenges.Where(kv => kv.Value.TenantId == tenantId).Select(kv => kv.Key).ToList();
        foreach (var kv in keys)
        {
            store.Remove(kv.Key);
        }
        foreach (var challengeId in challengeKeys)
            challenges.Remove(challengeId);
        return Task.FromResult(keys.Count);
    }
}

/// <summary>
/// AUTH009 (#738) — builds the one-time activation link (challenge id + token) and hands it to Notification
/// over the existing verification delivery seam for transient send. The token and the link that embeds it
/// exist only in memory here and in the outbound request — never persisted or logged. On any failure it
/// does not throw (the pending activation remains valid until it expires).
/// </summary>
public sealed class DaprNotificationAccountActivationSender(
    INotificationVerificationClient client,
    IOptions<AccountActivationOptions> options,
    ILogger<DaprNotificationAccountActivationSender> logger) : IAccountActivationSender
{
    public async Task SendAsync(
        string tenantId, string userId, string identityEmail, string challengeId, string token,
        CancellationToken cancellationToken = default)
    {
        var link = BuildLink(options.Value.ActivationBaseUrl, challengeId, token);
        try
        {
            await client.DeliverAsync(tenantId, identityEmail, link, cancellationToken);
        }
        catch (Exception)
        {
            // No token/link/address in the log. The pending activation stays valid until it expires.
            logger.LogWarning("Activation email delivery request to Notification failed. TenantId={TenantId}", tenantId);
        }
    }

    public static string BuildLink(string baseUrl, string challengeId, string token)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}cid={Uri.EscapeDataString(challengeId)}&token={Uri.EscapeDataString(token)}";
    }
}

/// <summary>
/// AUTH009 (#738) — durable activation security evidence: publishes outcome events to the Audit service
/// over pub/sub with a distinct <c>account-activation</c> category (kept separable from AUTH008B). The
/// actor is a SHA-256 hash of the user id; the token and email address are never included.
/// </summary>
public sealed class DaprAccountActivationAudit(DaprClient daprClient) : IAccountActivationAuditSink
{
    private const string PubSub = "fairspot-pubsub";
    private const string Topic = "security-events";
    private const string Category = "account-activation";

    public Task RequestedAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "requested", null, cancellationToken);
    public Task SucceededAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "succeeded", null, cancellationToken);
    public Task ExpiredAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "expired", null, cancellationToken);
    public Task FailedAsync(string tenantId, string userId, string reason, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "failed", reason, cancellationToken);
    public Task SupersededAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "superseded", null, cancellationToken);
    public Task RevokedAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "revoked", null, cancellationToken);

    private Task Publish(string tenantId, string userId, string outcome, string? reason, CancellationToken cancellationToken) =>
        daprClient.PublishEventAsync(PubSub, Topic,
            new SecurityAuditEvent(Category, outcome, tenantId, HashActor(userId), DateTimeOffset.UtcNow, reason),
            cancellationToken);

    private static string HashActor(string userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();
}
