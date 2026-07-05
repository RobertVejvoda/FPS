using FPS.Profile.Domain;

namespace FPS.Profile.Application;

/// <summary>One active email-verification record per (tenant, user); a new request supersedes the prior.</summary>
public interface IEmailVerificationRepository
{
    Task<EmailVerification?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task SaveAsync(EmailVerification verification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all email-verification records for a tenant (Profile-owned tenant data — deleted on tenant
    /// purge / sandbox reset so confidential address + secret-derived token-hash state is never orphaned).
    /// Returns the number of records removed; idempotent.
    /// </summary>
    Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}

/// <summary>Generates the cryptographically-random, URL-safe one-time verification token (plaintext).</summary>
public interface IVerificationTokenGenerator
{
    string Generate();
}

/// <summary>
/// Delivers the verification link/token to the recipient. AUTH008 slice 1 uses a logging stub that never
/// logs the token; slice 2 wires the real Profile→Notification/SendGrid send.
/// </summary>
public interface IEmailVerificationSender
{
    Task SendAsync(string tenantId, string userId, string emailAddress, string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Records durable verification security evidence (requested/succeeded/expired/failed) — outcome, reason,
/// and a pseudonymised actor only; never the token or the email address.
/// </summary>
public interface IEmailVerificationAuditSink
{
    Task RequestedAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task SucceededAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task ExpiredAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task FailedAsync(string tenantId, string userId, string reason, CancellationToken cancellationToken = default);
}

public sealed class EmailVerificationOptions
{
    public const string SectionName = "Profile:EmailVerification";
    public TimeSpan Ttl { get; init; } = TimeSpan.FromHours(24);
    public int MaxAttempts { get; init; } = 5;
    // AUTH008B #734 — base URL of the FairSpot email-verification callback page. The one-time token is
    // appended as a `token` query parameter to form the link delivered in the email.
    public string VerificationBaseUrl { get; init; } = "https://app.fairspot.net/verify-email";
}

public sealed record VerificationOutcome(bool Verified, string? RejectionReason)
{
    public static VerificationOutcome Ok() => new(true, null);
    public static VerificationOutcome Reject(string reason) => new(false, reason);
}
