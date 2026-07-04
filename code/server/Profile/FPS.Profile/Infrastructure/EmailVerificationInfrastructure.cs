using System.Security.Cryptography;
using Dapr.Client;
using FPS.Profile.Application;
using FPS.Profile.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Profile.Infrastructure;

public sealed class DaprEmailVerificationRepository(DaprClient daprClient) : IEmailVerificationRepository
{
    private const string StoreName = "profilestore";

    public Task<EmailVerification?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
        daprClient.GetStateAsync<EmailVerification?>(StoreName, Key(tenantId, userId), cancellationToken: cancellationToken);

    public Task SaveAsync(EmailVerification verification, CancellationToken cancellationToken = default) =>
        daprClient.SaveStateAsync(StoreName, Key(verification.TenantId, verification.UserId), verification, cancellationToken: cancellationToken);

    private static string Key(string tenantId, string userId) => $"email-verification:{tenantId}:{userId}";
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
}

/// <summary>URL-safe, high-entropy one-time token (256 bits, base64url).</summary>
public sealed class RandomVerificationTokenGenerator : IVerificationTokenGenerator
{
    public string Generate() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// AUTH008 slice 1 stub — records that a verification email was requested without ever logging the token
/// or address. Slice 2 replaces this with the Profile→Notification/SendGrid send.
/// </summary>
public sealed class LoggingEmailVerificationSender(ILogger<LoggingEmailVerificationSender> logger) : IEmailVerificationSender
{
    public Task SendAsync(string tenantId, string userId, string emailAddress, string token, CancellationToken cancellationToken = default)
    {
        // Never log the token or the address (both Secret/Confidential). Slice 2 delivers the real email.
        logger.LogInformation(
            "Email verification send requested (stub). TenantId={TenantId} — real delivery is AUTH008 slice 2 (#729).",
            tenantId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// AUTH008 slice 1 — emits verification security evidence via structured logs (no token, no address).
/// Slice 2 wires this to the Audit service over pub/sub.
/// </summary>
public sealed class LoggingEmailVerificationAuditSink(ILogger<LoggingEmailVerificationAuditSink> logger) : IEmailVerificationAuditSink
{
    public void Requested(string tenantId, string userId) => Emit(tenantId, userId, "requested", null);
    public void Succeeded(string tenantId, string userId) => Emit(tenantId, userId, "succeeded", null);
    public void Expired(string tenantId, string userId) => Emit(tenantId, userId, "expired", null);
    public void Failed(string tenantId, string userId, string reason) => Emit(tenantId, userId, "failed", reason);

    private void Emit(string tenantId, string userId, string outcome, string? reason) =>
        logger.LogInformation(
            "email-verification audit: Outcome={Outcome} Reason={Reason} TenantId={TenantId} UserId={UserId}",
            outcome, reason ?? "-", tenantId, userId);
}
