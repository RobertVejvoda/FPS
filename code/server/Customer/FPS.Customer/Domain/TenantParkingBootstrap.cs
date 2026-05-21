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

// Minimal validated policy snapshot stored as onboarding evidence.
// Contains the required fields from docs/business-layer/parking-policy-configuration.md.
public sealed record BootstrapPolicySnapshot(
    string TimeZone,
    string DrawCutOffTime,   // "HH:mm" 24-hour local time
    int DailyRequestCap,
    int AllocationLookbackDays,
    string RecordedByHash,
    DateTimeOffset RecordedAt)
{
    // Mirror the v1 cap limit from Configuration.Domain.ParkingPolicy.
    public const int V1DailyRequestCapLimit = 500;

    public static string? Validate(string timeZone, string drawCutOffTime, int dailyRequestCap, int allocationLookbackDays)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return "TimeZone is required.";
        // Use TimeOnly.TryParse so values like "99:99" are rejected even if they match HH:mm shape.
        if (string.IsNullOrWhiteSpace(drawCutOffTime) || !TimeOnly.TryParseExact(drawCutOffTime, "HH:mm", out _))
            return "DrawCutOffTime must be a valid HH:mm time (e.g. 18:00).";
        if (dailyRequestCap <= 0)
            return "DailyRequestCap must be greater than zero.";
        if (dailyRequestCap > V1DailyRequestCapLimit)
            return $"DailyRequestCap exceeds the v1 limit of {V1DailyRequestCapLimit}.";
        if (allocationLookbackDays < 0)
            return "AllocationLookbackDays must be non-negative.";
        return null;
    }
}

public sealed class TenantParkingBootstrap
{
    public string TenantId { get; init; } = string.Empty;
    public BootstrapPolicySnapshot? PolicySnapshot { get; private set; }
    public bool DefaultPolicyConfigured => PolicySnapshot is not null;
    private readonly Dictionary<string, BootstrapLocation> locations = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<BootstrapLocation> Locations => locations.Values.OrderBy(l => l.LocationId).ToList();

    public bool HasUsableLocation => locations.Values.Any(l => l.IsUsable);
    public bool IsComplete => DefaultPolicyConfigured && HasUsableLocation;

    public void RecordDefaultPolicy(BootstrapPolicySnapshot snapshot) =>
        PolicySnapshot = snapshot;

    public void RecordLocation(string locationId, int activeSlotCount, bool hasLocationPolicy, string actorHash) =>
        locations[locationId] = new BootstrapLocation(
            locationId, activeSlotCount, hasLocationPolicy, actorHash, DateTimeOffset.UtcNow);
}
