using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

internal sealed class TenantWorkspaceDto
{
    public string TenantId { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public List<TenantSupportContact> SupportContacts { get; set; } = [];
    public TenantLifecycleState LifecycleState { get; set; }
    public List<TenantStateTransition> Transitions { get; set; } = [];
    public TenantProvisioningMetadata Provisioning { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public TenantWorkspace ToDomain() => TenantWorkspace.Restore(
        TenantId, Slug, DisplayName, Region, TimeZone, SupportContacts,
        LifecycleState, Transitions, Provisioning, CreatedAt, UpdatedAt);

    public static TenantWorkspaceDto FromDomain(TenantWorkspace ws) => new()
    {
        TenantId = ws.TenantId,
        Slug = ws.Slug,
        DisplayName = ws.DisplayName,
        Region = ws.Region,
        TimeZone = ws.TimeZone,
        SupportContacts = ws.SupportContacts.ToList(),
        LifecycleState = ws.LifecycleState,
        Transitions = ws.Transitions.ToList(),
        Provisioning = ws.Provisioning,
        CreatedAt = ws.CreatedAt,
        UpdatedAt = ws.UpdatedAt,
    };
}

internal sealed class TenantIdentityConfigDto
{
    public string TenantId { get; set; } = string.Empty;
    public string TrustedIssuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string TenantClaimName { get; set; } = string.Empty;
    public string SubjectClaimName { get; set; } = string.Empty;
    public List<string> RoleClaimNames { get; set; } = [];
    public Dictionary<string, string> RoleMapping { get; set; } = [];
    public bool LocalAccountPolicyEnabled { get; set; }
    public string ConfiguredByHash { get; set; } = string.Empty;
    public DateTimeOffset ConfiguredAt { get; set; }

    public TenantIdentityConfig ToDomain() => new()
    {
        TenantId = TenantId,
        TrustedIssuer = TrustedIssuer,
        Audience = Audience,
        TenantClaimName = TenantClaimName,
        SubjectClaimName = SubjectClaimName,
        RoleClaimNames = RoleClaimNames,
        RoleMapping = new Dictionary<string, string>(RoleMapping, StringComparer.OrdinalIgnoreCase),
        LocalAccountPolicyEnabled = LocalAccountPolicyEnabled,
        ConfiguredByHash = ConfiguredByHash,
        ConfiguredAt = ConfiguredAt,
    };

    public static TenantIdentityConfigDto FromDomain(TenantIdentityConfig c) => new()
    {
        TenantId = c.TenantId,
        TrustedIssuer = c.TrustedIssuer,
        Audience = c.Audience,
        TenantClaimName = c.TenantClaimName,
        SubjectClaimName = c.SubjectClaimName,
        RoleClaimNames = c.RoleClaimNames.ToList(),
        RoleMapping = c.RoleMapping.ToDictionary(kv => kv.Key, kv => kv.Value),
        LocalAccountPolicyEnabled = c.LocalAccountPolicyEnabled,
        ConfiguredByHash = c.ConfiguredByHash,
        ConfiguredAt = c.ConfiguredAt,
    };
}

internal sealed class TenantParkingBootstrapDto
{
    public string TenantId { get; set; } = string.Empty;
    public BootstrapPolicySnapshot? PolicySnapshot { get; set; }
    public List<BootstrapLocation> Locations { get; set; } = [];

    public TenantParkingBootstrap ToDomain()
    {
        var bootstrap = new TenantParkingBootstrap { TenantId = TenantId };
        if (PolicySnapshot is not null)
            bootstrap.RecordDefaultPolicy(PolicySnapshot);
        foreach (var loc in Locations)
            bootstrap.RecordLocation(loc.LocationId, loc.ActiveSlotCount, loc.HasLocationPolicy, loc.RecordedByHash);
        return bootstrap;
    }

    public static TenantParkingBootstrapDto FromDomain(TenantParkingBootstrap b) => new()
    {
        TenantId = b.TenantId,
        PolicySnapshot = b.PolicySnapshot,
        Locations = b.Locations.ToList(),
    };
}
