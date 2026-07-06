namespace FPS.Profile.Domain;

/// <summary>
/// AUTH009 (#738) — Identity/Profile-owned pending-account activation state for a tenant-scoped user
/// provisioned or invited as inactive. One active record per (tenant, user); a new challenge supersedes
/// any prior one (overwriting the hash + challenge id), invalidating the old link. Stores only the
/// <b>hash</b> of the one-time activation token — never the plaintext — plus the opaque challenge id, the
/// identity email under proof, expiry, attempt count, and lifecycle state.
///
/// This is deliberately SEPARATE from AUTH008B <see cref="EmailVerification"/>: AUTH009 proves ownership
/// of the login/identity email to activate a pending account (pre-auth, token+challenge keyed), while
/// AUTH008B verifies a changed operational notification address for an already-active, authenticated user.
/// </summary>
public sealed class AccountActivation
{
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    // Opaque, unguessable id carried in the activation link alongside the token. Identifies the record
    // for the anonymous confirm path without the caller supplying tenant/user/email.
    public string ChallengeId { get; init; } = string.Empty;
    // Normalised (trimmed, lower-cased) identity email this activation proves ownership of.
    public string IdentityEmail { get; init; } = string.Empty;
    // SHA-256 hex of the one-time token. Secret-derived; the plaintext is never stored.
    public string TokenHash { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public int AttemptCount { get; private set; }
    public AccountActivationState State { get; private set; } = AccountActivationState.Pending;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ActivatedAt { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void RegisterFailedAttempt() => AttemptCount++;

    public void MarkExpired() => State = AccountActivationState.Expired;

    public void MarkSuperseded() => State = AccountActivationState.Superseded;

    public void MarkRevoked() => State = AccountActivationState.Revoked;

    public void MarkActivated(DateTimeOffset now)
    {
        State = AccountActivationState.Activated;
        ActivatedAt = now;
    }

    /// <summary>
    /// True only when this record is activated for the given (normalised) identity email — the notification
    /// recipient trust seam (#728) uses this so an operational-address change automatically drops trust and
    /// AUTH008B verification applies again.
    /// </summary>
    public bool IsActivatedFor(string normalisedIdentityEmail) =>
        State == AccountActivationState.Activated &&
        string.Equals(IdentityEmail, normalisedIdentityEmail, StringComparison.OrdinalIgnoreCase);
}

public enum AccountActivationState
{
    Pending,
    Activated,
    Expired,
    Failed,
    Superseded,
    Revoked,
}
