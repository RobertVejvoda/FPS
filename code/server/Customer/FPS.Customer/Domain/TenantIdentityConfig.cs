namespace FPS.Customer.Domain;

public sealed class TenantIdentityConfig
{
    public string TenantId { get; init; } = string.Empty;
    public string TrustedIssuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string TenantClaimName { get; init; } = string.Empty;
    public string SubjectClaimName { get; init; } = string.Empty;
    public IReadOnlyList<string> RoleClaimNames { get; init; } = [];
    // Maps raw IdP group/role → FPS role name; unmapped groups are ignored.
    public IReadOnlyDictionary<string, string> RoleMapping { get; init; } =
        new Dictionary<string, string>();
    // When true, local break-glass accounts may be registered for this tenant.
    public bool LocalAccountPolicyEnabled { get; init; }
    // Keycloak identity-provider broker alias for this tenant's company SSO (AUTH011).
    // Non-secret routing metadata: it names a broker configuration in the FairSpot
    // realm so the web can send kc_idp_hint. It never grants access by itself.
    public string? IdpBrokerAlias { get; init; }
    public string ConfiguredByHash { get; init; } = string.Empty;
    public DateTimeOffset ConfiguredAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public TenantIdentityConfig WithUpdate(string updatedByHash) =>
        new()
        {
            TenantId = TenantId,
            TrustedIssuer = TrustedIssuer,
            Audience = Audience,
            TenantClaimName = TenantClaimName,
            SubjectClaimName = SubjectClaimName,
            RoleClaimNames = RoleClaimNames,
            RoleMapping = RoleMapping,
            LocalAccountPolicyEnabled = LocalAccountPolicyEnabled,
            IdpBrokerAlias = IdpBrokerAlias,
            ConfiguredByHash = ConfiguredByHash,
            ConfiguredAt = ConfiguredAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
}
