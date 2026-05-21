namespace FPS.Customer.Domain;

public sealed class TenantWorkspace
{
    private readonly List<TenantStateTransition> transitions = [];

    public string TenantId { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public IReadOnlyList<TenantSupportContact> SupportContacts { get; set; } = [];
    public TenantLifecycleState LifecycleState { get; private set; } = TenantLifecycleState.Draft;
    public IReadOnlyList<TenantStateTransition> Transitions => transitions.AsReadOnly();
    public TenantProvisioningMetadata Provisioning { get; init; } = new();
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

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

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
