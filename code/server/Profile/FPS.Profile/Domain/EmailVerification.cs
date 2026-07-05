namespace FPS.Profile.Domain;

/// <summary>
/// AUTH008 (#729) — email ownership verification state for a FairSpot-local account, one active record
/// per (tenant, user). Stores only the <b>hash</b> of the one-time verifier — never the plaintext token —
/// plus expiry, attempt count, the address under verification, and the lifecycle state. A new request
/// supersedes any prior record (overwriting the hash), which invalidates the old token; an email change
/// verifies a different address, so a previously-verified record no longer matches (see IsVerifiedFor).
/// </summary>
public sealed class EmailVerification
{
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    // Normalised (trimmed, lower-cased) address this verification is for.
    public string EmailAddress { get; init; } = string.Empty;
    // SHA-256 hex of the one-time token. Secret-derived; the plaintext is never stored.
    public string TokenHash { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public int AttemptCount { get; private set; }
    public EmailVerificationState State { get; private set; } = EmailVerificationState.Pending;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? VerifiedAt { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void RegisterFailedAttempt() => AttemptCount++;

    public void MarkExpired() => State = EmailVerificationState.Expired;

    public void MarkVerified(DateTimeOffset now)
    {
        State = EmailVerificationState.Verified;
        VerifiedAt = now;
    }

    /// <summary>True only when this record is verified for the given (normalised) address — the seam
    /// recipient resolution (#728) uses so an address change automatically drops trust.</summary>
    public bool IsVerifiedFor(string normalisedAddress) =>
        State == EmailVerificationState.Verified &&
        string.Equals(EmailAddress, normalisedAddress, StringComparison.OrdinalIgnoreCase);
}

public enum EmailVerificationState
{
    Pending,
    Verified,
    Expired,
    Failed,
}
