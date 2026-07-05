using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using FPS.Profile.Domain;
using Microsoft.Extensions.Options;

namespace FPS.Profile.Application;

/// <summary>
/// AUTH008 (#729) — the FairSpot-local email-ownership verification flow. Issues a one-time token
/// (stored only as a hash), sends it out-of-band, and marks the address verified when the correct token
/// is presented before expiry and within the attempt limit. Fails closed on expired/wrong/reused/
/// superseded tokens. The token plaintext exists only transiently for delivery — never persisted or
/// returned — and never appears in audit evidence.
/// </summary>
public sealed class EmailVerificationService(
    IEmailVerificationRepository repository,
    IVerificationTokenGenerator tokenGenerator,
    IEmailVerificationSender sender,
    IEmailVerificationAuditSink audit,
    TimeProvider timeProvider,
    IOptions<EmailVerificationOptions> options)
{
    public async Task<string?> RequestAsync(
        string tenantId, string userId, string emailAddress, CancellationToken cancellationToken = default)
    {
        var normalised = Normalise(emailAddress);
        if (normalised is null)
            return "email_invalid";

        var token = tokenGenerator.Generate();
        var now = timeProvider.GetUtcNow();

        // Overwriting the record at (tenant, user) supersedes and invalidates any prior token.
        var verification = new EmailVerification
        {
            TenantId = tenantId,
            UserId = userId,
            EmailAddress = normalised,
            TokenHash = Hash(token),
            ExpiresAt = now + options.Value.Ttl,
            CreatedAt = now,
        };
        await repository.SaveAsync(verification, cancellationToken);

        // Out-of-band delivery of the plaintext token. Nothing logs it.
        await sender.SendAsync(tenantId, userId, normalised, token, cancellationToken);
        await audit.RequestedAsync(tenantId, userId, cancellationToken);
        return null;
    }

    public async Task<VerificationOutcome> ConfirmAsync(
        string tenantId, string userId, string token, CancellationToken cancellationToken = default)
    {
        var verification = await repository.GetAsync(tenantId, userId, cancellationToken);
        if (verification is null || verification.State != EmailVerificationState.Pending)
        {
            await audit.FailedAsync(tenantId, userId, "no_pending_verification", cancellationToken);
            return VerificationOutcome.Reject("no_pending_verification");
        }

        var now = timeProvider.GetUtcNow();
        if (verification.IsExpired(now))
        {
            verification.MarkExpired();
            await repository.SaveAsync(verification, cancellationToken);
            await audit.ExpiredAsync(tenantId, userId, cancellationToken);
            return VerificationOutcome.Reject("expired");
        }

        if (verification.AttemptCount >= options.Value.MaxAttempts)
        {
            await audit.FailedAsync(tenantId, userId, "too_many_attempts", cancellationToken);
            return VerificationOutcome.Reject("too_many_attempts");
        }

        if (string.IsNullOrWhiteSpace(token) || !HashesMatch(Hash(token), verification.TokenHash))
        {
            verification.RegisterFailedAttempt();
            await repository.SaveAsync(verification, cancellationToken);
            await audit.FailedAsync(tenantId, userId, "invalid_token", cancellationToken);
            return VerificationOutcome.Reject("invalid_token");
        }

        verification.MarkVerified(now);
        await repository.SaveAsync(verification, cancellationToken);
        await audit.SucceededAsync(tenantId, userId, cancellationToken);
        return VerificationOutcome.Ok();
    }

    /// <summary>Normalises an address for storage/compare, or null when it is not a valid email.</summary>
    public static string? Normalise(string? emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress)) return null;
        var trimmed = emailAddress.Trim();
        if (trimmed.Any(char.IsWhiteSpace) || !trimmed.Contains('@', StringComparison.Ordinal))
            return null;
        try
        {
            var parsed = new MailAddress(trimmed);
            if (!string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
                return null;
            return parsed.Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool HashesMatch(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
