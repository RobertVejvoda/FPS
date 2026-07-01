using System.Text.RegularExpressions;

namespace FPS.Customer.Domain;

public sealed class TenantWorkspace
{
    // Each label: starts/ends with alnum, middle may contain hyphens (RFC 1123).
    // TLD: at least two alnum chars. No scheme, path, port, wildcard, or whitespace.
    private static readonly Regex HostnamePattern =
        new(@"^([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9]{2,}$", RegexOptions.Compiled);
    private readonly List<TenantStateTransition> transitions = [];
    private readonly List<TenantDiscoveryDomain> discoveryDomains = [];
    private readonly List<TenantDemoSeedEvent> seedEvents = [];

    public string TenantId { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public IReadOnlyList<TenantSupportContact> SupportContacts { get; set; } = [];
    public TenantKind Kind { get; init; } = TenantKind.Production;
    // PLAT003A — durable, defense-in-depth marker for the resettable evaluation sandbox
    // (Green Logistics). A destructive sandbox reset requires Kind==Sandbox AND this flag; a
    // normal customer tenant leaves it false and can never be reset by the platform reset path.
    // Read only from stored metadata — a caller can never pass it in a reset request.
    public bool IsResettableSandbox { get; init; }
    public TenantLifecycleState LifecycleState { get; private set; } = TenantLifecycleState.Draft;
    public IReadOnlyList<TenantStateTransition> Transitions => transitions.AsReadOnly();
    public IReadOnlyList<TenantDemoSeedEvent> SeedEvents => seedEvents.AsReadOnly();
    public TenantProvisioningMetadata Provisioning { get; init; } = new();
    public TenantBrandingConfig Branding { get; private set; } = new();
    public IReadOnlyList<TenantDiscoveryDomain> DiscoveryDomains => discoveryDomains.AsReadOnly();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public string? TryTransition(TenantLifecycleState to, string actorId, string? reason, string? evidence)
    {
        if (!IsValidTransition(LifecycleState, to))
            return $"Transition from {LifecycleState} to {to} is not permitted.";

        var transition = new TenantStateTransition(LifecycleState, to, actorId, DateTimeOffset.UtcNow, reason, evidence);
        transitions.Add(transition);
        LifecycleState = to;
        UpdatedAt = transition.OccurredAt;
        return null;
    }

    public string? SetBranding(TenantBrandingConfig config)
    {
        var error = TenantBrandingConfig.Validate(config);
        if (error is not null) return error;
        Branding = config;
        Touch();
        return null;
    }

    public string? AddDiscoveryDomain(string domain, string actorHash)
    {
        var normalized = NormalizeDomain(domain);
        if (!IsValidDomainFormat(normalized))
            return "Domain format is invalid. Expected a hostname such as 'example.com'.";
        if (discoveryDomains.Any(d => d.Domain == normalized))
            return $"Domain '{normalized}' is already registered for this tenant.";
        discoveryDomains.Add(new TenantDiscoveryDomain(normalized, actorHash, DateTimeOffset.UtcNow));
        Touch();
        return null;
    }

    public bool RemoveDiscoveryDomain(string domain)
    {
        var normalized = NormalizeDomain(domain);
        var index = discoveryDomains.FindIndex(d => d.Domain == normalized);
        if (index < 0) return false;
        discoveryDomains.RemoveAt(index);
        Touch();
        return true;
    }

    public void RecordSeedEvent(string actorHash, string datasetVersion, string reason)
    {
        seedEvents.Add(new TenantDemoSeedEvent(actorHash, datasetVersion, DateTimeOffset.UtcNow, reason));
        Touch();
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    internal static TenantWorkspace Restore(
        string tenantId, string slug, string displayName, string region, string timeZone,
        IReadOnlyList<TenantSupportContact> supportContacts,
        TenantKind kind,
        bool isResettableSandbox,
        TenantLifecycleState lifecycleState,
        IReadOnlyList<TenantStateTransition> storedTransitions,
        TenantProvisioningMetadata provisioning,
        TenantBrandingConfig branding,
        IReadOnlyList<TenantDiscoveryDomain> storedDiscoveryDomains,
        IReadOnlyList<TenantDemoSeedEvent> storedSeedEvents,
        DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        var ws = new TenantWorkspace
        {
            TenantId = tenantId, Slug = slug, DisplayName = displayName,
            Region = region, TimeZone = timeZone, SupportContacts = supportContacts,
            Kind = kind, IsResettableSandbox = isResettableSandbox, Provisioning = provisioning, CreatedAt = createdAt,
        };
        ws.transitions.AddRange(storedTransitions);
        ws.LifecycleState = lifecycleState;
        ws.Branding = branding;
        ws.discoveryDomains.AddRange(storedDiscoveryDomains);
        ws.seedEvents.AddRange(storedSeedEvents);
        ws.UpdatedAt = updatedAt;
        return ws;
    }

    private static string NormalizeDomain(string domain) =>
        domain.Trim().ToLowerInvariant();

    private static bool IsValidDomainFormat(string domain) =>
        !string.IsNullOrEmpty(domain) && HostnamePattern.IsMatch(domain);

    private static bool IsValidTransition(TenantLifecycleState from, TenantLifecycleState to) =>
        (from, to) switch
        {
            (TenantLifecycleState.Draft, TenantLifecycleState.Configured) => true,
            (TenantLifecycleState.Draft, TenantLifecycleState.Suspended) => true,
            (TenantLifecycleState.Draft, TenantLifecycleState.Archived) => true,
            (TenantLifecycleState.Configured, TenantLifecycleState.Seeded) => true,
            (TenantLifecycleState.Configured, TenantLifecycleState.Suspended) => true,
            (TenantLifecycleState.Configured, TenantLifecycleState.Archived) => true,
            (TenantLifecycleState.Seeded, TenantLifecycleState.Ready) => true,
            (TenantLifecycleState.Seeded, TenantLifecycleState.Suspended) => true,
            (TenantLifecycleState.Seeded, TenantLifecycleState.Archived) => true,
            (TenantLifecycleState.Ready, TenantLifecycleState.Suspended) => true,
            (TenantLifecycleState.Ready, TenantLifecycleState.Archived) => true,
            (TenantLifecycleState.Suspended, TenantLifecycleState.Draft) => true,
            (TenantLifecycleState.Suspended, TenantLifecycleState.Configured) => true,
            (TenantLifecycleState.Suspended, TenantLifecycleState.Seeded) => true,
            (TenantLifecycleState.Suspended, TenantLifecycleState.Ready) => true,
            (TenantLifecycleState.Suspended, TenantLifecycleState.Archived) => true,
            _ => false,
        };
}
