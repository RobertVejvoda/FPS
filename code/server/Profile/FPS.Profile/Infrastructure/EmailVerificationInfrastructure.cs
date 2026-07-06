using System.Security.Cryptography;
using System.Text;
using Dapr.Client;
using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.SharedKernel.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FPS.Profile.Infrastructure;

public sealed class DaprEmailVerificationRepository(DaprClient daprClient) : IEmailVerificationRepository
{
    private const string StoreName = "profilestore";

    public Task<EmailVerification?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        daprClient.GetStateAsync<EmailVerification>(StoreName, Key(tenantId, userId), cancellationToken: cancellationToken);

    public async Task SaveAsync(EmailVerification verification, CancellationToken cancellationToken = default)
    {
        await daprClient.SaveStateAsync(
            StoreName, Key(verification.TenantId, verification.UserId), verification, cancellationToken: cancellationToken);
        await AddToTenantIndexAsync(verification.TenantId, verification.UserId, cancellationToken);
    }

    // Tenant purge / sandbox reset removes every verification record for the tenant so no confidential
    // address / token-hash state is left orphaned when the owning profiles are purged.
    public async Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var indexKey = TenantIndexKey(tenantId);
        var userIds = await daprClient.GetStateAsync<List<string>>(StoreName, indexKey, cancellationToken: cancellationToken) ?? [];

        var removed = 0;
        foreach (var userId in userIds)
        {
            var key = Key(tenantId, userId);
            var record = await daprClient.GetStateAsync<EmailVerification>(StoreName, key, cancellationToken: cancellationToken);
            if (record is null)
                continue; // stale index entry — nothing to remove
            await daprClient.DeleteStateAsync(StoreName, key, cancellationToken: cancellationToken);
            removed++;
        }

        await daprClient.DeleteStateAsync(StoreName, indexKey, cancellationToken: cancellationToken);
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

    // Use the shared tenant storage-key utility (sanitised tenant segment) — same contract as
    // DaprProfileRepository — rather than ad-hoc interpolation (tenant-storage-contract.md).
    private static string Key(string tenantId, string userId) =>
        TenantStorageKey.For("email-verification", tenantId, userId);

    private static string TenantIndexKey(string tenantId) =>
        TenantStorageKey.For("email-verification-index", tenantId, "all");
}

public sealed class InMemoryEmailVerificationRepository : IEmailVerificationRepository
{
    private readonly Dictionary<string, EmailVerification> store = new(StringComparer.Ordinal);

    public Task<EmailVerification?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.GetValueOrDefault($"{tenantId}:{userId}"));

    public Task SaveAsync(EmailVerification verification, CancellationToken cancellationToken = default)
    {
        store[$"{verification.TenantId}:{verification.UserId}"] = verification;
        return Task.CompletedTask;
    }

    public Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var keys = store.Where(kv => kv.Value.TenantId == tenantId).Select(kv => kv.Key).ToList();
        foreach (var key in keys) store.Remove(key);
        return Task.FromResult(keys.Count);
    }
}

/// <summary>URL-safe, high-entropy one-time token (256 bits, base64url).</summary>
public sealed class RandomVerificationTokenGenerator : IVerificationTokenGenerator
{
    public string Generate() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// AUTH008B (#734) — real delivery: builds the one-time verification link from the configured base URL and
/// the token, then hands it to Notification over Dapr service invocation for transient send. The token
/// (and the link that embeds it) exists only in memory here and in the outbound request — never persisted
/// or logged. On any failure it does not throw (the pending verification remains valid until it expires).
/// </summary>
public sealed class DaprNotificationEmailVerificationSender(
    INotificationVerificationClient client,
    IOptions<EmailVerificationOptions> options,
    ILogger<DaprNotificationEmailVerificationSender> logger) : IEmailVerificationSender
{
    public async Task SendAsync(string tenantId, string userId, string emailAddress, string token, CancellationToken cancellationToken = default)
    {
        var link = BuildLink(options.Value.VerificationBaseUrl, token);
        try
        {
            await client.DeliverAsync(tenantId, emailAddress, link, cancellationToken);
        }
        catch (Exception)
        {
            // No token/link/address in the log. The pending verification stays valid until it expires.
            logger.LogWarning("Verification email delivery request to Notification failed. TenantId={TenantId}", tenantId);
        }
    }

    public static string BuildLink(string baseUrl, string token)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }
}

/// <summary>Seam over the Dapr service invocation to Notification, so the sender's link handling is unit-testable.</summary>
public interface INotificationVerificationClient
{
    Task DeliverAsync(string tenantId, string emailAddress, string verificationLink, CancellationToken cancellationToken = default);
}

public sealed class DaprNotificationVerificationClient(DaprClient daprClient) : INotificationVerificationClient
{
    private const string NotificationAppId = "fairspot-notification";
    private const string DeliverMethod = "internal/notification/email-verification";

    public Task DeliverAsync(string tenantId, string emailAddress, string verificationLink, CancellationToken cancellationToken = default) =>
        daprClient.InvokeMethodAsync<VerificationEmailDeliveryRequest, VerificationEmailDeliveryResult>(
            NotificationAppId, DeliverMethod,
            new VerificationEmailDeliveryRequest(tenantId, emailAddress, verificationLink), cancellationToken);
}

// Dapr service-invocation contract mirroring Notification's internal endpoint (matched by JSON shape).
public sealed record VerificationEmailDeliveryRequest(string TenantId, string EmailAddress, string VerificationLink);
public sealed record VerificationEmailDeliveryResult(bool Sent);

/// <summary>
/// AUTH008B (#734) — durable verification security evidence: publishes outcome events to the Audit service
/// over pub/sub. The actor is a SHA-256 hash of the user id (pseudonymised); the token and email address
/// are never included.
/// </summary>
public sealed class DaprEmailVerificationAudit(DaprClient daprClient) : IEmailVerificationAuditSink
{
    private const string PubSub = "fairspot-pubsub";
    private const string Topic = "security-events";
    private const string Category = "email-verification";

    public Task RequestedAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "requested", null, cancellationToken);
    public Task SucceededAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "succeeded", null, cancellationToken);
    public Task ExpiredAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "expired", null, cancellationToken);
    public Task FailedAsync(string tenantId, string userId, string reason, CancellationToken cancellationToken = default) =>
        Publish(tenantId, userId, "failed", reason, cancellationToken);

    private Task Publish(string tenantId, string userId, string outcome, string? reason, CancellationToken cancellationToken) =>
        daprClient.PublishEventAsync(PubSub, Topic,
            new SecurityAuditEvent(Category, outcome, tenantId, HashActor(userId), DateTimeOffset.UtcNow, reason),
            cancellationToken);

    private static string HashActor(string userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();
}

/// <summary>Security audit evidence. Actor is a hash; carries outcome/reason only — never token or email.</summary>
public sealed record SecurityAuditEvent(
    string Category, string Outcome, string TenantId, string ActorHash, DateTimeOffset OccurredAt, string? Reason);
