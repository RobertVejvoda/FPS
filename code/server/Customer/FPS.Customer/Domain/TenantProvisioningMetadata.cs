using System.Text.RegularExpressions;

namespace FPS.Customer.Domain;

public sealed record TenantProvisioningMetadata
{
    public string TenantSlug { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }

    // Deterministic, tenant-scoped collection/store names derived from slug.
    // These are evidence for operators — no actual provisioning occurs here.
    public IReadOnlyDictionary<string, string> ServiceCollections { get; init; } =
        new Dictionary<string, string>();

    private static readonly Regex SafeSlug = new(@"[^a-z0-9\-]", RegexOptions.Compiled);

    public static TenantProvisioningMetadata Generate(string tenantId, string slug)
    {
        var safe = SafeSlug.Replace(slug.ToLowerInvariant(), "-").Trim('-');
        var collections = new Dictionary<string, string>
        {
            ["customer"] = $"fps-{safe}-tenants",
            ["booking"] = $"fps-{safe}-bookings",
            ["notification"] = $"fps-{safe}-notifications",
            ["profile"] = $"fps-{safe}-profiles",
            ["audit"] = $"fps-{safe}-audit",
            ["configuration"] = $"fps-{safe}-configuration",
            ["reporting"] = $"fps-{safe}-reporting",
        };
        return new TenantProvisioningMetadata
        {
            TenantId = tenantId,
            TenantSlug = safe,
            GeneratedAt = DateTimeOffset.UtcNow,
            ServiceCollections = collections,
        };
    }
}
