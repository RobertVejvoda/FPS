using FPS.Profile.Domain;

namespace FPS.Profile.Application;

/// <summary>
/// One active account-activation record per (tenant, user); a new challenge supersedes the prior. A
/// separate challenge-id index lets the anonymous confirm path resolve the record from the opaque
/// challenge id alone, without trusting a caller-supplied tenant/user/email.
/// </summary>
public interface IAccountActivationRepository
{
    Task<AccountActivation?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Resolves (tenantId, userId) from the opaque challenge id, or null when unknown.</summary>
    Task<(string TenantId, string UserId)?> ResolveChallengeAsync(string challengeId, CancellationToken cancellationToken = default);

    Task SaveAsync(AccountActivation activation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all account-activation records (and challenge index entries) for a tenant on tenant purge /
    /// sandbox reset so confidential email + secret-derived token-hash state is never orphaned. Idempotent;
    /// returns the number of records removed.
    /// </summary>
    Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}

/// <summary>Delivers the activation link/token to the invited identity email. Never logs the token/link.</summary>
public interface IAccountActivationSender
{
    Task SendAsync(string tenantId, string userId, string identityEmail, string challengeId, string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Durable activation security evidence (requested/succeeded/expired/invalid/too-many-attempts/superseded/
/// revoked/completed) — outcome, reason, and a pseudonymised actor only; never the token or the email.
/// </summary>
public interface IAccountActivationAuditSink
{
    Task RequestedAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task SucceededAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task ExpiredAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task FailedAsync(string tenantId, string userId, string reason, CancellationToken cancellationToken = default);
    Task SupersededAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task RevokedAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
}

public sealed class AccountActivationOptions
{
    public const string SectionName = "Profile:AccountActivation";
    public TimeSpan Ttl { get; init; } = TimeSpan.FromHours(72);
    public int MaxAttempts { get; init; } = 5;
    // AUTH009 (#738) — base URL of the FairSpot account-activation callback page. The opaque challenge id
    // and the one-time token are appended as query parameters to form the link delivered in the email.
    public string ActivationBaseUrl { get; init; } = "https://app.fairspot.net/activate";
}

/// <summary>Outcome of an activation confirm attempt.</summary>
public sealed record ActivationOutcome(bool Activated, string? RejectionReason)
{
    public static ActivationOutcome Ok() => new(true, null);
    public static ActivationOutcome Reject(string reason) => new(false, reason);
}

/// <summary>Result of issuing an activation challenge (the challenge id is opaque, not secret; the token is never returned).</summary>
public sealed record ActivationChallengeResult(bool Issued, string? ChallengeId, string? RejectionReason)
{
    public static ActivationChallengeResult Ok(string challengeId) => new(true, challengeId, null);
    public static ActivationChallengeResult Reject(string reason) => new(false, null, reason);
}
