using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public sealed class TenantDemoSeedService(
    ITenantRepository tenantRepository,
    IDemoSeedProfileClient profileClient,
    IDemoSeedConfigurationClient configClient)
{
    private const string DatasetVersion = "gl-v1";

    private static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..16];

    public async Task<(DemoSeedResult? result, string? error)> SeedAsync(
        string tenantId, string actorId, string authorizationHeader, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(tenantId, ct);
        if (tenant is null) return (null, "Tenant not found.");

        if (tenant.Kind is not (TenantKind.Sandbox or TenantKind.Evaluation))
            return (null, $"Demo seed is only available for Sandbox and Evaluation tenants. This tenant is {tenant.Kind}.");

        var seedAt = DateTimeOffset.UtcNow;

        var (profilesSeeded, profileError) = await profileClient.SeedAsync(
            authorizationHeader, GreenLogisticsDataset.Employees, ct);
        if (profileError is not null)
            return (null, $"Profile seed failed: {profileError}");

        var (slotsSeeded, configError) = await configClient.SeedAsync(
            authorizationHeader, GreenLogisticsDataset.LocationId,
            GreenLogisticsDataset.Slots, GreenLogisticsDataset.Policy, ct);
        if (configError is not null)
            return (null, $"Configuration seed failed: {configError}");

        tenant.RecordSeedEvent(Hash(actorId), DatasetVersion, "demo-seed");
        await tenantRepository.SaveAsync(tenant, ct);

        return (new DemoSeedResult(
            tenantId, DatasetVersion, seedAt, profilesSeeded, slotsSeeded,
            GapReport:
            [
                "Booking draw history not seeded — run a draw manually after seed.",
                "DataHub projections not seeded — they are built from live draw events.",
                "Notification history not seeded — notifications are sent on live events only.",
            ]), null);
    }
}

public sealed record DemoSeedResult(
    string TenantId,
    string DatasetVersion,
    DateTimeOffset SeedAt,
    int ProfilesSeeded,
    int SlotsSeeded,
    IReadOnlyList<string> GapReport);

// ── Green Logistics canonical demo dataset ─────────────────────────────────────
// Stable user IDs and slot IDs are intentional — they must match on every reset
// so foreign key references (e.g. company-car reserved slot) stay consistent.
internal static class GreenLogisticsDataset
{
    internal const string LocationId = "GL-HQ";

    // Company-car employee — has a fixed reserved slot below.
    private const string AliceUserId = "a1a10001-0001-0001-0001-000000000001";

    internal static readonly IReadOnlyList<DemoEmployeeRecord> Employees =
    [
        new(AliceUserId, "Alice Novák",      ["employee"],              null, LocationId, true,  true,  false, false,
            [new("gl-veh-001", "3GL-AA01", "car",        IsElectric: false, IsDefault: true)]),
        new("b2b20002-0002-0002-0002-000000000002", "Bob Dvořák",      ["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-002", "3GL-BB02", "car",        IsElectric: true,  IsDefault: true)]),
        new("c3c30003-0003-0003-0003-000000000003", "Carol Horáček",   ["employee"],              null, LocationId, true,  false, true,  false,
            [new("gl-veh-003", "3GL-CC03", "car",        IsElectric: false, IsDefault: true)]),
        new("d4d40004-0004-0004-0004-000000000004", "David Navrátil",  ["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-004", "3GL-DD04", "car",        IsElectric: false, IsDefault: true),
             new("gl-veh-005", "3GL-DD05", "motorcycle", IsElectric: false, IsDefault: false)]),
        new("e5e50005-0005-0005-0005-000000000005", "Eva Procházka",   ["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-006", "3GL-EE06", "car",        IsElectric: false, IsDefault: true)]),
        new("f6f60006-0006-0006-0006-000000000006", "Frank Kratochvíl",["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-007", "3GL-FF07", "car",        IsElectric: false, IsDefault: true)]),
        new("a7a70007-0007-0007-0007-000000000007", "Gabi Krejčí",     ["employee"],              null, LocationId, false, false, false, false, []),
        new("h8h80008-0008-0008-0008-000000000008", "Hana Pokorná",    ["employee","hr_manager"], null, LocationId, true,  false, false, false,
            [new("gl-veh-008", "3GL-HH08", "car",        IsElectric: false, IsDefault: true)]),
        new("i9i90009-0009-0009-0009-000000000009", "Ivan Blažek",     ["employee","admin"],      null, LocationId, true,  false, false, false,
            [new("gl-veh-009", "3GL-II09", "car",        IsElectric: false, IsDefault: true)]),
    ];

    internal static readonly IReadOnlyList<DemoSlotRecord> Slots =
    [
        // 13 standard active slots
        new("gl-slot-001", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-002", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-003", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-004", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-005", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-006", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-007", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-008", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-009", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-010", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-011", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-012", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-013", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        // EV charger slot
        new("gl-slot-014", IsActive: true,  HasCharger: true,  IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        // Accessibility slot
        new("gl-slot-015", IsActive: true,  HasCharger: false, IsAccessible: true,  IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        // Company-car reserved slot (Alice Novák)
        new("gl-slot-016", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: true,  IsMotorcycleCapacity: false, ReservedForUserId: AliceUserId),
        // Motorcycle area (4 motorcycles share this slot)
        new("gl-slot-017", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: true,  null, MotorcycleCapacityUnits: 4),
        // 3 additional standard slots
        new("gl-slot-018", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-019", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("gl-slot-020", IsActive: true,  HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
    ];

    internal static readonly DemoPolicyRecord Policy = new(
        TimeZone: "Europe/Prague",
        DrawCutOffTime: new TimeOnly(18, 0),
        DailyRequestCap: 50,
        AllocationLookbackDays: 30,
        LateCancellationPenalty: 1,
        NoShowPenalty: 3,
        ManualAdjustmentEnabled: false,
        SameDayBookingEnabled: false,
        SameDayUsesRequestCap: false,
        AutomaticReallocationEnabled: false,
        UsageConfirmationRequired: false,
        UsageConfirmationWindowMinutes: 0,
        UsageConfirmationMethods: [],
        NoShowDetectionEnabled: false,
        CompanyCarTier1Enabled: true,
        CompanyCarOverflowBehavior: "reject");
}

// Internal payload records — these mirror the endpoint contracts for Profile and
// Configuration without importing those assemblies.
public sealed record DemoEmployeeRecord(
    string UserId,
    string? DisplayName,
    IReadOnlyList<string> FpsRoles,
    string? NotificationAddress,
    string? HomeLocationId,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible,
    IReadOnlyList<DemoVehicleRecord> Vehicles);

public sealed record DemoVehicleRecord(
    string VehicleId,
    string LicensePlate,
    string VehicleType,
    bool IsElectric,
    bool IsDefault = false);

public sealed record DemoSlotRecord(
    string SlotId,
    bool IsActive,
    bool HasCharger,
    bool IsAccessible,
    bool IsCompanyCarOnly,
    bool IsMotorcycleCapacity,
    string? ReservedForUserId,
    int? MotorcycleCapacityUnits = null);

public sealed record DemoPolicyRecord(
    string TimeZone,
    TimeOnly DrawCutOffTime,
    int DailyRequestCap,
    int AllocationLookbackDays,
    int LateCancellationPenalty,
    int NoShowPenalty,
    bool ManualAdjustmentEnabled,
    bool SameDayBookingEnabled,
    bool SameDayUsesRequestCap,
    bool AutomaticReallocationEnabled,
    bool UsageConfirmationRequired,
    int UsageConfirmationWindowMinutes,
    IReadOnlyList<string> UsageConfirmationMethods,
    bool NoShowDetectionEnabled,
    bool CompanyCarTier1Enabled,
    string CompanyCarOverflowBehavior);
