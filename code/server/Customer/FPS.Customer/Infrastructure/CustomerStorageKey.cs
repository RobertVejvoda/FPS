using System.Text.RegularExpressions;

namespace FPS.Customer.Infrastructure;

internal static class CustomerStorageKey
{
    private static readonly Regex ValidPattern = new(@"^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", RegexOptions.Compiled);
    private static readonly string[] ReservedPrefixes = ["fps-", "dapr-", "admin-", "system-"];

    internal static string Sanitise(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var normalised = tenantId.Trim().ToLowerInvariant();
        if (normalised.Length < 3)
            throw new ArgumentException($"Tenant ID '{tenantId}' is too short (minimum 3 characters).", nameof(tenantId));
        if (normalised.Length > 63)
            throw new ArgumentException($"Tenant ID '{tenantId}' exceeds the 63-character maximum.", nameof(tenantId));
        if (!ValidPattern.IsMatch(normalised))
            throw new ArgumentException($"Tenant ID '{tenantId}' contains invalid characters.", nameof(tenantId));
        foreach (var prefix in ReservedPrefixes)
            if (normalised.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException($"Tenant ID '{tenantId}' uses reserved prefix '{prefix}'.", nameof(tenantId));
        return normalised;
    }

    internal static string Tenant(string tenantId) => $"tenant:{Sanitise(tenantId)}";
    internal static string TenantSlug(string slug) => $"tenant:slug:{slug.Trim().ToLowerInvariant()}";
    internal static string IdentityConfig(string tenantId) => $"identity:config:{Sanitise(tenantId)}";
    internal static string IdentityAdmins(string tenantId) => $"identity:admins:{Sanitise(tenantId)}";
    internal static string Bootstrap(string tenantId) => $"bootstrap:{Sanitise(tenantId)}";
    internal static string IdentityIndex() => "identity:index";
    internal static string TenantIndex() => "tenant:index";
    internal static string DiscoveryDomain(string domain) => $"tenant:discovery-domain:{domain.Trim().ToLowerInvariant()}";
    internal static string SandboxResetLease() => "sandbox-reset:lease";
    internal static string SandboxResetEvidence(string tenantId) => $"sandbox-reset:evidence:{Sanitise(tenantId)}";
}
