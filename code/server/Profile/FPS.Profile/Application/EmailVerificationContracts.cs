using FPS.Profile.Domain;

namespace FPS.Profile.Application;

/// <summary>One active email-verification record per (tenant, user); a new request supersedes the prior.</summary>
public interface IEmailVerificationRepository
{
    Task<EmailVerification?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task SaveAsync(EmailVerification verification, CancellationToken cancellationToken = default);
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

/// <summary>Records verification security evidence (requested/succeeded/expired/failed) — never the token.</summary>
public interface IEmailVerificationAuditSink
{
    void Requested(string tenantId, string userId);
    void Succeeded(string tenantId, string userId);
    void Expired(string tenantId, string userId);
    void Failed(string tenantId, string userId, string reason);
}

public sealed class EmailVerificationOptions
{
    public const string SectionName = "Profile:EmailVerification";
    public TimeSpan Ttl { get; init; } = TimeSpan.FromHours(24);
    public int MaxAttempts { get; init; } = 5;
}

public sealed record VerificationOutcome(bool Verified, string? RejectionReason)
{
    public static VerificationOutcome Ok() => new(true, null);
    public static VerificationOutcome Reject(string reason) => new(false, reason);
}
