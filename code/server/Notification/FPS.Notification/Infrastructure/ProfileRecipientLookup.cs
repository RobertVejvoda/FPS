using Dapr.Client;

namespace FPS.Notification.Infrastructure;

/// <summary>
/// NOTIF #728 — the raw cross-service lookup of a recipient user ID to a verified email via Profile.
/// Kept behind an interface so the resolver's orchestration is unit-testable (DaprClient's typed
/// invocation helper is non-virtual and cannot be mocked directly). Transport failures propagate so
/// the resolver can fail closed.
/// </summary>
public interface IProfileRecipientLookup
{
    Task<ProfileRecipientResult> LookupAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
}

public sealed record ProfileRecipientResult(bool Resolved, string? Email, string? Reason);

/// <summary>Invokes Profile's internal <c>notification-recipient</c> endpoint over Dapr service invocation.</summary>
public sealed class DaprProfileRecipientLookup(DaprClient daprClient) : IProfileRecipientLookup
{
    private const string ProfileAppId = "fairspot-profile";
    private const string ResolveMethod = "internal/profile/notification-recipient";

    public async Task<ProfileRecipientResult> LookupAsync(
        string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var response = await daprClient.InvokeMethodAsync<NotificationRecipientRequest, NotificationRecipientResult>(
            ProfileAppId, ResolveMethod, new NotificationRecipientRequest(tenantId, userId), cancellationToken);
        return new ProfileRecipientResult(response.Resolved, response.Email, response.Reason);
    }
}

// Dapr service-invocation contract mirroring Profile's internal endpoint (matched by JSON shape).
public sealed record NotificationRecipientRequest(string TenantId, string UserId);

public sealed record NotificationRecipientResult(bool Resolved, string? Email, string? Reason);
