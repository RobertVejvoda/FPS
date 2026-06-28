using System.Text.RegularExpressions;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Customer.Domain;

public sealed record TenantProvisioningMetadata
{
    public string TenantSlug { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }

    // PLAT002: durable evidence of the per-service tenant storage scopes. Keys are the bounded-
    // context service names; values are the deterministic, sanitised collection/partition names
    // derived centrally from the trusted tenant id (TenantStorageScope) — so this evidence
    // matches exactly what a tenant purge addresses. No actual provisioning occurs here.
    public IReadOnlyDictionary<string, string> ServiceCollections { get; init; } =
        new Dictionary<string, string>();

    private static readonly Regex SafeSlug = new(@"[^a-z0-9\-]", RegexOptions.Compiled);

    public static string Sanitize(string? slug) =>
        slug is null ? string.Empty : SafeSlug.Replace(slug.Trim().ToLowerInvariant(), "-").Trim('-');

    public static TenantProvisioningMetadata Generate(string tenantId, string slug)
    {
        // Scope names derive from the canonical tenant id — the same value services key their
        // Dapr/storage by (request:{tenantId}:...) and that a tenant purge scopes by — so this
        // evidence matches the purge targets and existing storage exactly. The tenant id must
        // satisfy the storage contract (validated at provisioning); TenantStorageScope enforces it.
        var collections = TenantStorageScope.Services.ToDictionary(
            service => service,
            service => TenantStorageScope.Collection(service, tenantId),
            StringComparer.Ordinal);

        return new TenantProvisioningMetadata
        {
            TenantId = tenantId,
            TenantSlug = Sanitize(slug),
            GeneratedAt = DateTimeOffset.UtcNow,
            ServiceCollections = collections,
        };
    }
}
