using System.Text.RegularExpressions;

namespace FPS.Booking.Infrastructure;

/// <summary>
/// Produces sanitised, tenant-scoped Dapr state store keys.
/// Key format: {entity-type}:{tenantId}:{entity-id}
/// See: docs/production/tenant-storage-contract.md
/// </summary>
public static class TenantStorageKey
{
    private static readonly Regex ValidPattern = new(@"^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", RegexOptions.Compiled);

    private static readonly string[] ReservedPrefixes = ["fps-", "dapr-", "admin-", "system-"];

    /// <summary>
    /// Returns a sanitised tenant ID string suitable for use as a key segment.
    /// Throws <see cref="ArgumentException"/> if the id violates the contract.
    /// </summary>
    public static string Sanitise(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var normalised = tenantId.Trim().ToLowerInvariant();

        if (normalised.Length < 3)
            throw new ArgumentException($"Tenant ID '{tenantId}' is too short (minimum 3 characters).", nameof(tenantId));

        if (normalised.Length > 63)
            throw new ArgumentException($"Tenant ID '{tenantId}' exceeds the 63-character maximum.", nameof(tenantId));

        if (!ValidPattern.IsMatch(normalised))
            throw new ArgumentException(
                $"Tenant ID '{tenantId}' contains invalid characters. Only lowercase alphanumeric and hyphens are allowed, " +
                "with no leading or trailing hyphens.", nameof(tenantId));

        foreach (var prefix in ReservedPrefixes)
        {
            if (normalised.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Tenant ID '{tenantId}' uses reserved prefix '{prefix}'.", nameof(tenantId));
        }

        return normalised;
    }

    /// <summary>
    /// Builds a tenant-scoped Dapr state key: {entityType}:{tenantId}:{entityId}
    /// </summary>
    public static string For(string entityType, string tenantId, string entityId)
        => $"{entityType}:{Sanitise(tenantId)}:{entityId}";

    /// <summary>
    /// Builds a tenant-scoped Dapr state key: {entityType}:{tenantId}:{entityId}
    /// </summary>
    public static string For(string entityType, string tenantId, Guid entityId)
        => For(entityType, tenantId, entityId.ToString());
}
