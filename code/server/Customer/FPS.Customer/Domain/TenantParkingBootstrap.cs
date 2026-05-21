namespace FPS.Customer.Domain;

public sealed record BootstrapLocation(
    string LocationId,
    int ActiveSlotCount,
    bool HasLocationPolicy,
    string RecordedByHash,
    DateTimeOffset RecordedAt)
{
    public bool IsUsable => ActiveSlotCount > 0;
}

public sealed class TenantParkingBootstrap
{
    public string TenantId { get; init; } = string.Empty;
    public bool DefaultPolicyConfigured { get; private set; }
    public string? PolicyRecordedByHash { get; private set; }
    public DateTimeOffset? PolicyRecordedAt { get; private set; }
    private readonly Dictionary<string, BootstrapLocation> locations = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<BootstrapLocation> Locations => locations.Values.OrderBy(l => l.LocationId).ToList();

    public bool HasUsableLocation => locations.Values.Any(l => l.IsUsable);
    public bool IsComplete => DefaultPolicyConfigured && HasUsableLocation;

    public void RecordDefaultPolicy(string actorHash)
    {
        DefaultPolicyConfigured = true;
        PolicyRecordedByHash = actorHash;
        PolicyRecordedAt = DateTimeOffset.UtcNow;
    }

    public void RecordLocation(string locationId, int activeSlotCount, bool hasLocationPolicy, string actorHash) =>
        locations[locationId] = new BootstrapLocation(
            locationId, activeSlotCount, hasLocationPolicy, actorHash, DateTimeOffset.UtcNow);
}
