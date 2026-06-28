using System.Text.RegularExpressions;
using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public sealed partial class TenantRequestService(
    ITenantRequestRepository repository,
    ITurnstileVerifier turnstile,
    ITenantRequestNotifier notifier)
{
    private const int MaxMessageLength = 2000;

    /// <summary>
    /// Public intake. Verifies the Turnstile token, then records a <see cref="TenantRequest"/> and
    /// alerts sales. No tenant is provisioned. Returns a friendly error and creates no record when
    /// the token fails or input is invalid, so the unauthenticated path cannot be abused.
    /// </summary>
    public async Task<(TenantRequest? request, string? error)> SubmitAsync(
        string? company, string? primaryDomain, string? contactEmail, string? message,
        string? turnstileToken, string? remoteIp, CancellationToken ct)
    {
        company = company?.Trim() ?? string.Empty;
        primaryDomain = NormaliseDomain(primaryDomain);
        contactEmail = contactEmail?.Trim().ToLowerInvariant() ?? string.Empty;
        message = (message ?? string.Empty).Trim();

        if (company.Length is 0 or > 200) return (null, "Company name is required.");
        if (!EmailPattern().IsMatch(contactEmail)) return (null, "A valid contact email is required.");
        if (!DomainPattern().IsMatch(primaryDomain)) return (null, "A valid primary domain is required.");
        if (message.Length > MaxMessageLength) message = message[..MaxMessageLength];

        // Fail closed: an unverified token never creates a record.
        if (!await turnstile.VerifyAsync(turnstileToken, remoteIp, ct))
            return (null, "Could not verify the request. Please try again.");

        // Soft anti-abuse: collapse repeat submissions from the same prospect while one is open.
        if (await repository.HasOpenRequestForEmailAsync(contactEmail, ct))
            return (null, "A request from this email is already being reviewed. We'll be in touch.");

        var request = new TenantRequest
        {
            RequestId = Guid.NewGuid().ToString("n"),
            Company = company,
            PrimaryDomain = primaryDomain,
            ContactEmail = contactEmail,
            Message = message,
            Status = TenantRequestStatus.Requested,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await repository.SaveAsync(request, ct);
        await notifier.NotifySalesAsync(request, ct);
        return (request, null);
    }

    /// <summary>Platform-operator queue. Newest first.</summary>
    public async Task<IReadOnlyList<TenantRequest>> ListAsync(CancellationToken ct)
    {
        var all = await repository.ListAsync(ct);
        return all.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public Task<(TenantRequest? request, string? error)> ApproveAsync(
        string requestId, string actorHash, string? reason, CancellationToken ct) =>
        DecideAsync(requestId, TenantRequestStatus.Approved, actorHash, reason, ct);

    public Task<(TenantRequest? request, string? error)> RejectAsync(
        string requestId, string actorHash, string? reason, CancellationToken ct) =>
        DecideAsync(requestId, TenantRequestStatus.Rejected, actorHash, reason, ct);

    private async Task<(TenantRequest? request, string? error)> DecideAsync(
        string requestId, TenantRequestStatus decision, string actorHash, string? reason, CancellationToken ct)
    {
        var existing = await repository.GetAsync(requestId, ct);
        if (existing is null) return (null, "Request not found.");
        if (existing.Status != TenantRequestStatus.Requested)
            return (null, $"Request is already {existing.Status.ToString().ToLowerInvariant()}.");

        var decided = existing with
        {
            Status = decision,
            DecidedAt = DateTimeOffset.UtcNow,
            DecidedByHash = actorHash,
            DecisionReason = reason?.Trim(),
        };
        await repository.SaveAsync(decided, ct);
        return (decided, null);
    }

    private static string NormaliseDomain(string? domain)
    {
        var d = (domain ?? string.Empty).Trim().ToLowerInvariant();
        if (d.StartsWith("https://", StringComparison.Ordinal)) d = d[8..];
        else if (d.StartsWith("http://", StringComparison.Ordinal)) d = d[7..];
        return d.TrimEnd('/').Trim();
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^(?!-)[a-z0-9-]{1,63}(\.[a-z0-9-]{1,63})+$")]
    private static partial Regex DomainPattern();
}
