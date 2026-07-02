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
            authorizationHeader, tenantId, GreenLogisticsDataset.Employees, ct);
        if (profileError is not null)
            return (null, $"Profile seed failed: {profileError}");

        var (slotsSeeded, configError) = await configClient.SeedAsync(
            authorizationHeader, tenantId, GreenLogisticsDataset.LocationId,
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

// ── Green Logistics showcase demo dataset (DEMOSEED003 / #704) ──────────────────
// A small, legible evaluation showcase: 10 named people and 6 named, business-readable
// slots — understandable in one screen without inspecting a large roster. Stable user IDs
// and slot IDs are intentional so they match on every reset and foreign-key references
// (the company-car reserved slot) stay consistent. This mirrors the local harness seed
// (tools/dev-seed.sh); bulk/load-test data lives on the explicit perf-seed path, never here.
//
// The provisioning path seeds profiles + slots + policy only. Fairness *history* (recent
// allocations, penalties) and the reallocation finale are produced by live draws in the
// local harness seed, not fabricated here — see the GapReport returned by SeedAsync.
internal static class GreenLogisticsDataset
{
    internal const string LocationId = "GL-HQ";

    // Company-car employee — guaranteed the fixed VIP-01 slot (Tier-1) outside the lottery.
    private const string CompanyCarUserId = "a1a10001-0001-0001-0001-000000000001";

    // The showcase roster. Four special-need personas (company-car, EV, accessible,
    // motorcycle) plus six general drivers who compete in the fair lottery for the two
    // general slots — enough demand for a visible waitlist. Realistic CZ plates, no diacritics.
    internal static readonly IReadOnlyList<DemoEmployeeRecord> Employees =
    [
        // Company-car (VIP-01, Tier-1 fixed). Non-electric so it needs no charger.
        new(CompanyCarUserId, "Jan Novak",            ["employee"],              null, LocationId, true,  true,  false, false,
            [new("gl-veh-001", "1AB 2345", "car",        IsElectric: false, IsDefault: true)]),
        // EV driver — prefers the charger slot (EV-01).
        new("b2b20002-0002-0002-0002-000000000002", "Petra Svobodova",  ["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-002", "2SC 4417", "car",        IsElectric: true,  IsDefault: true)]),
        // Accessibility need — requires the accessible slot (ACC-01).
        new("c3c30003-0003-0003-0003-000000000003", "Hana Vesela",      ["employee"],              null, LocationId, true,  false, true,  false,
            [new("gl-veh-003", "5BL 6628", "car",        IsElectric: false, IsDefault: true)]),
        // Motorcycle — only the motorcycle area (MOTO-01) fits it.
        new("d4d40004-0004-0004-0004-000000000004", "Tomas Dvorak",     ["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-004", "3AH 8820", "motorcycle", IsElectric: false, IsDefault: true)]),
        // General drivers — compete for the two general slots (A-01, A-02).
        new("e5e50005-0005-0005-0005-000000000005", "Pavel Cerny",      ["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-005", "4EK 1193", "car",        IsElectric: false, IsDefault: true)]),
        new("f6f60006-0006-0006-0006-000000000006", "Martin Horak",     ["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-006", "1AP 3092", "car",        IsElectric: false, IsDefault: true)]),
        // Recent frequent winner — the local seed gives this persona recent allocation history.
        new("a7a70007-0007-0007-0007-000000000007", "Jana Kucerova",    ["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-007", "6CT 7741", "car",        IsElectric: false, IsDefault: true)]),
        // Penalised persona — the local seed gives this persona an active late-cancellation penalty.
        new("b8b80008-0008-0008-0008-000000000008", "Petr Novotny",     ["employee"],              null, LocationId, true,  false, false, false,
            [new("gl-veh-008", "7AZ 2284", "car",        IsElectric: false, IsDefault: true)]),
        // Unlucky history — no recent wins, so the highest fair weight going into the draw.
        new("c9c90009-0009-0009-0009-000000000009", "Lucie Prochazkova",["employee","hr_manager"], null, LocationId, true,  false, false, false,
            [new("gl-veh-009", "3BM 9087", "car",        IsElectric: false, IsDefault: true)]),
        new("d0d00010-0010-0010-0010-000000000010", "Karel Urban",      ["employee","admin"],      null, LocationId, true,  false, false, false,
            [new("gl-veh-010", "4EH 4451", "car",        IsElectric: false, IsDefault: true)]),
    ];

    // Six named, business-readable slots. IsMotorcycleCapacity units = 1 keeps MOTO-01 a single
    // named unit so the layout reads as exactly six slots.
    internal static readonly IReadOnlyList<DemoSlotRecord> Slots =
    [
        new("A-01",    IsActive: true, HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("A-02",    IsActive: true, HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("EV-01",   IsActive: true, HasCharger: true,  IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("ACC-01",  IsActive: true, HasCharger: false, IsAccessible: true,  IsCompanyCarOnly: false, IsMotorcycleCapacity: false, null),
        new("MOTO-01", IsActive: true, HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false, IsMotorcycleCapacity: true,  null, MotorcycleCapacityUnits: 1),
        new("VIP-01",  IsActive: true, HasCharger: false, IsAccessible: false, IsCompanyCarOnly: true,  IsMotorcycleCapacity: false, ReservedForUserId: CompanyCarUserId),
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
        // Cancelling an allocated request promotes the next fair waitlisted driver — this drives
        // the local seed's reallocation finale.
        AutomaticReallocationEnabled: true,
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
