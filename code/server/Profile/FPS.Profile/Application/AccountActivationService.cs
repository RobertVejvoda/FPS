using System.Security.Cryptography;
using System.Text;
using FPS.Profile.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace FPS.Profile.Application;

/// <summary>
/// AUTH009 (#738) — pending-account activation gate. An admin/provisioning path issues a one-time
/// activation challenge (stored only as a hash) for an existing inactive user; the user activates by
/// presenting the challenge id + token through a pre-auth confirm path. Until activation succeeds the
/// user stays <see cref="ProfileStatus.Inactive"/> and blocked by the shared deactivation gate. Fails
/// closed on expired/wrong/reused/superseded/revoked challenges and cross-tenant/unknown challenge ids.
/// The token plaintext exists only transiently for delivery — never persisted, returned, or logged — and
/// never appears in audit evidence. Kept strictly separate from AUTH008B <see cref="EmailVerificationService"/>.
/// </summary>
public sealed class AccountActivationService(
    IAccountActivationRepository repository,
    IProfileRepository profiles,
    IDeactivatedUserStore deactivatedUsers,
    IVerificationTokenGenerator tokenGenerator,
    IAccountActivationSender sender,
    IAccountActivationAuditSink audit,
    TimeProvider timeProvider,
    IOptions<AccountActivationOptions> options)
{
    /// <summary>
    /// Issues (or refreshes) an activation challenge for an existing inactive user in the given tenant.
    /// The tenant/user are resolved by the authenticated admin caller — never from the confirm side.
    /// </summary>
    public async Task<ActivationChallengeResult> IssueAsync(
        string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
            return ActivationChallengeResult.Reject("invalid_request");

        var profile = await profiles.GetAsync(tenantId, userId, cancellationToken);
        if (profile is null)
            return ActivationChallengeResult.Reject("user_not_found");
        if (profile.IsActive)
            return ActivationChallengeResult.Reject("already_active");

        var identityEmail = EmailVerificationService.Normalise(profile.NotificationAddress);
        if (identityEmail is null)
            return ActivationChallengeResult.Reject("email_invalid");

        var now = timeProvider.GetUtcNow();

        // Supersede any prior pending challenge for this user so the old link can no longer activate.
        var existing = await repository.GetAsync(tenantId, userId, cancellationToken);
        if (existing is { State: AccountActivationState.Pending })
        {
            existing.MarkSuperseded();
            await repository.SaveAsync(existing, cancellationToken);
            await audit.SupersededAsync(tenantId, userId, cancellationToken);
        }

        var token = tokenGenerator.Generate();
        var challengeId = NewChallengeId();
        var activation = new AccountActivation
        {
            TenantId = tenantId,
            UserId = userId,
            ChallengeId = challengeId,
            IdentityEmail = identityEmail,
            TokenHash = Hash(token),
            ExpiresAt = now + options.Value.Ttl,
            CreatedAt = now,
        };
        await repository.SaveAsync(activation, cancellationToken);

        // A pending user must be blocked until activation — keep the shared deactivation gate closed.
        deactivatedUsers.Deactivate(tenantId, userId);

        // Out-of-band delivery of the plaintext token (embedded in the link). Nothing logs it.
        await sender.SendAsync(tenantId, userId, identityEmail, challengeId, token, cancellationToken);
        await audit.RequestedAsync(tenantId, userId, cancellationToken);
        return ActivationChallengeResult.Ok(challengeId);
    }

    /// <summary>
    /// Confirms an activation from the opaque challenge id + token alone. The caller supplies no trusted
    /// identity; (tenant, user) are resolved from the stored challenge. On success the account is activated
    /// and the shared deactivation gate is reopened.
    /// </summary>
    public async Task<ActivationOutcome> ConfirmAsync(
        string challengeId, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(token))
            return ActivationOutcome.Reject("invalid_request");

        var resolved = await repository.ResolveChallengeAsync(challengeId, cancellationToken);
        if (resolved is null)
            return ActivationOutcome.Reject("invalid_challenge");
        var (tenantId, userId) = resolved.Value;

        var activation = await repository.GetAsync(tenantId, userId, cancellationToken);
        // Fail closed on unknown, superseded (record moved to a newer challenge), or already-terminal state.
        if (activation is null ||
            !string.Equals(activation.ChallengeId, challengeId, StringComparison.Ordinal) ||
            activation.State != AccountActivationState.Pending)
        {
            await audit.FailedAsync(tenantId, userId, "no_pending_activation", cancellationToken);
            return ActivationOutcome.Reject("invalid_challenge");
        }

        var now = timeProvider.GetUtcNow();
        if (activation.IsExpired(now))
        {
            activation.MarkExpired();
            await repository.SaveAsync(activation, cancellationToken);
            await audit.ExpiredAsync(tenantId, userId, cancellationToken);
            return ActivationOutcome.Reject("expired");
        }

        if (activation.AttemptCount >= options.Value.MaxAttempts)
        {
            await audit.FailedAsync(tenantId, userId, "too_many_attempts", cancellationToken);
            return ActivationOutcome.Reject("too_many_attempts");
        }

        if (!HashesMatch(Hash(token), activation.TokenHash))
        {
            activation.RegisterFailedAttempt();
            await repository.SaveAsync(activation, cancellationToken);
            await audit.FailedAsync(tenantId, userId, "invalid_token", cancellationToken);
            return ActivationOutcome.Reject("invalid_token");
        }

        activation.MarkActivated(now);
        await repository.SaveAsync(activation, cancellationToken);

        // Activate the account: flip the profile to Active and reopen the shared deactivation gate.
        var profile = await profiles.GetAsync(tenantId, userId, cancellationToken);
        if (profile is not null && !profile.IsActive)
            await profiles.SaveAsync(WithStatus(profile, ProfileStatus.Active), cancellationToken);
        deactivatedUsers.Reactivate(tenantId, userId);

        await audit.SucceededAsync(tenantId, userId, cancellationToken);
        return ActivationOutcome.Ok();
    }

    /// <summary>Revokes a pending activation challenge (admin path). No-op unless a pending record exists.</summary>
    public async Task<bool> RevokeAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var activation = await repository.GetAsync(tenantId, userId, cancellationToken);
        if (activation is not { State: AccountActivationState.Pending })
            return false;

        activation.MarkRevoked();
        await repository.SaveAsync(activation, cancellationToken);
        await audit.RevokedAsync(tenantId, userId, cancellationToken);
        return true;
    }

    private static UserProfile WithStatus(UserProfile p, ProfileStatus status) => new()
    {
        TenantId = p.TenantId,
        UserId = p.UserId,
        Status = status,
        ParkingEligible = p.ParkingEligible,
        HasCompanyCar = p.HasCompanyCar,
        AccessibilityEligible = p.AccessibilityEligible,
        ReservedSpaceEligible = p.ReservedSpaceEligible,
        Vehicles = p.Vehicles,
        EmployeeId = p.EmployeeId,
        DisplayName = p.DisplayName,
        FpsRoles = p.FpsRoles,
        NotificationAddress = p.NotificationAddress,
        HomeLocationId = p.HomeLocationId,
        SnapshotVersion = p.SnapshotVersion,
        UpdatedAt = p.UpdatedAt,
        FactSource = p.FactSource,
    };

    private static string NewChallengeId() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool HashesMatch(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
