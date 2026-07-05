using System.Net.Mail;
using FPS.Profile.Application;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

/// <summary>
/// NOTIF #728 — internal (service-invocation only) resolution of a notification recipient user ID to
/// a <b>verified</b> email address for the Notification service. Employee booking events carry user
/// IDs, not email addresses; Notification calls this over Dapr to obtain a trusted delivery address.
///
/// Verified = an Active profile with a well-formed <c>NotificationAddress</c> whose <c>FactSource</c>
/// is a trusted provisioning source (SSO claims, admin entry/seed, or authorized HR/file import).
/// FairSpot-local self-verification is out of scope (#729). Everything else fails closed with a safe
/// reason and no PII is returned in the reason or logged here — the caller records a delivery-rejected
/// outcome.
/// </summary>
[ApiController]
[DaprInternalOnly]
// Internal service-to-service endpoint — kept out of the public OpenAPI/generated web client, matching
// the other Dapr-only controllers (e.g. PurgeController). The web frontend never calls this.
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class NotificationRecipientController(
    IProfileRepository repository,
    IEmailVerificationRepository emailVerifications) : ControllerBase
{
    // Must match the FactSource values actually written by trusted Profile provisioning paths:
    // sso-claims (ProfileSnapshotController), admin-seed (ProfileAdminController), admin-entry +
    // file-import (EmployeeBootstrapController), hr-import (HrImportService). demo-seed is intentionally
    // excluded (synthetic showcase data), as is FairSpot-local self-verification (#729).
    private static readonly HashSet<string> TrustedFactSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "sso-claims",
        "admin-seed",
        "admin-entry",
        "hr-import",
        "file-import",
    };

    [HttpPost("/internal/profile/notification-recipient")]
    [ProducesResponseType(typeof(NotificationRecipientResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(
        [FromBody] NotificationRecipientRequest request, CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.TenantId) ||
            string.IsNullOrWhiteSpace(request.UserId))
        {
            return Ok(NotificationRecipientResult.Reject("invalid_request"));
        }

        // Tenant-scoped lookup enforces isolation: a (tenant, userId) pair only resolves its own tenant.
        var profile = await repository.GetAsync(request.TenantId, request.UserId, cancellationToken);
        if (profile is null || !profile.IsActive)
            return Ok(NotificationRecipientResult.Reject("recipient_not_found"));

        if (string.IsNullOrWhiteSpace(profile.NotificationAddress))
            return Ok(NotificationRecipientResult.Reject("no_verified_email"));

        if (!IsWellFormedEmail(profile.NotificationAddress))
            return Ok(NotificationRecipientResult.Reject("email_malformed"));

        // Trusted when the address comes from a trusted provisioning source OR the user has completed
        // FairSpot-local email ownership verification for this exact address (AUTH008 #729). An address
        // change drops trust automatically because the verification records the address it verified.
        if (TrustedFactSources.Contains(profile.FactSource))
            return Ok(NotificationRecipientResult.Accept(profile.NotificationAddress));

        var normalised = Application.EmailVerificationService.Normalise(profile.NotificationAddress);
        if (normalised is not null)
        {
            var verification = await emailVerifications.GetAsync(request.TenantId, request.UserId, cancellationToken);
            if (verification is not null && verification.IsVerifiedFor(normalised))
                return Ok(NotificationRecipientResult.Accept(profile.NotificationAddress));
        }

        return Ok(NotificationRecipientResult.Reject("email_unverified_source"));
    }

    private static bool IsWellFormedEmail(string candidate)
    {
        var trimmed = candidate.Trim();
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace) || !trimmed.Contains('@', StringComparison.Ordinal))
            return false;
        try
        {
            var parsed = new MailAddress(trimmed);
            return string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record NotificationRecipientRequest(string TenantId, string UserId);

public sealed record NotificationRecipientResult(bool Resolved, string? Email, string? Reason)
{
    public static NotificationRecipientResult Accept(string email) => new(true, email, null);
    public static NotificationRecipientResult Reject(string reason) => new(false, null, reason);
}
