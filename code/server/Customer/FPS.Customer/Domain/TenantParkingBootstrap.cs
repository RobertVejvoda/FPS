using System.Text.RegularExpressions;

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
    private static readonly Regex TimePattern = new(@"^\d{2}:\d{2}$", RegexOptions.Compiled);

    public static string? Validate(string timeZone, string drawCutOffTime, int dailyRequestCap, int allocationLookbackDays)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return "TimeZone is required.";
        if (string.IsNullOrWhiteSpace(drawCutOffTime) || !TimePattern.IsMatch(drawCutOffTime))
            return "DrawCutOffTime must be in HH:mm format (e.g. 18:00).";
        if (dailyRequestCap < 1)
            return "DailyRequestCap must be at least 1.";
        if (allocationLookbackDays < 1)
            return "AllocationLookbackDays must be at least 1.";
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
