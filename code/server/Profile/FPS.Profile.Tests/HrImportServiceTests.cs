using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace FPS.Profile.Tests;

/// <summary>
/// Tests for HrImportService covering employees-only and employees+vehicles scenarios.
/// </summary>
public sealed class HrImportServiceTests
{
    private readonly InMemoryProfileRepository profileRepo = new();
    private readonly InMemoryDeactivatedUserStore deactivatedStore = new();
    private readonly HrImportService service;

    private static readonly string EmployeeHeader =
        "external_subject,display_name,email,roles,home_location,preferred_zone,parking_eligible,has_company_car,accessibility_eligible,reserved_space_eligible,active";
    private static readonly string VehicleHeader =
        "external_subject,vehicle_alias,vehicle_license_plate,vehicle_type,vehicle_is_electric,active";

    public HrImportServiceTests()
    {
        var currentUser = new StaticCurrentUser("actor-1", "tenant-test");
        service = new HrImportService(
            profileRepo, deactivatedStore, currentUser, NullLogger<HrImportService>.Instance);
    }

    private static Stream CsvStream(params string[] lines) =>
        new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

    // ── Employees-only (backward compat) ─────────────────────────────────────

    [Fact]
    public async Task Preview_EmployeesOnly_ReturnsEmployeeRowsAndEmptyVehicles()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");

        var (preview, error) = await service.PreviewAsync("t1", emp, null, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(preview);
        Assert.Single(preview!.Rows);
        Assert.Equal(1, preview.Created);
        Assert.Empty(preview.VehicleRows);
        Assert.Equal(0, preview.VehiclesValid);
        Assert.Equal(0, preview.VehiclesRejected);
    }

    [Fact]
    public async Task Commit_EmployeesOnly_AppliesRowsAndReturnsZeroVehicles()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");

        var (result, error) = await service.CommitAsync("t1", emp, null, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(1, result!.Applied);
        Assert.Equal(0, result.Rejected);
        Assert.Equal(0, result.VehiclesApplied);
        Assert.Equal(0, result.VehiclesRejected);
        Assert.Empty(result.VehicleErrors);
    }

    // ── Employees + vehicles happy path ──────────────────────────────────────

    [Fact]
    public async Task Preview_EmployeesPlusVehicles_ReturnsSeparateCounts()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true",
            "emp2,Bob,bob@c.com,employee,Prague,,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,Daily Car,1AA 1111,car,false,true",
            "emp2,EV,2BB 2222,car,true,true");

        var (preview, error) = await service.PreviewAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(preview);
        Assert.Equal(2, preview!.Created);
        Assert.Equal(2, preview.VehiclesValid);
        Assert.Equal(0, preview.VehiclesRejected);
        Assert.Equal(2, preview.VehicleRows.Count);
        Assert.All(preview.VehicleRows, r => Assert.Equal(HrVehicleImportStatus.Valid, r.Status));
    }

    [Fact]
    public async Task Commit_EmployeesPlusVehicles_PersistsVehiclesOnProfiles()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,Daily Car,1AA 1111,car,false,true");

        var (result, error) = await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(1, result!.Applied);
        Assert.Equal(1, result.VehiclesApplied);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        Assert.NotNull(profile);
        var vehicle = Assert.Single(profile!.Vehicles);
        Assert.Equal("1AA 1111", vehicle.LicensePlate); // spaces preserved, letters uppercased
        Assert.Equal("car", vehicle.VehicleType);
        Assert.False(vehicle.IsElectric);
        Assert.True(vehicle.IsActive);
    }

    [Fact]
    public async Task Commit_VehicleForExistingProfile_AddsVehicleToExistingEmployee()
    {
        // Pre-register employee outside of this import batch.
        var bootstrapService = new EmployeeBootstrapService(profileRepo, deactivatedStore);
        await bootstrapService.RegisterAsync("t1", new BootstrapEmployeeRequest(
            "existing-emp", null, true, ["employee"], null, "Prague",
            true, false, false, false, "admin-seed"), CancellationToken.None);

        // Import only vehicles — employee is an existing profile, not in this batch.
        var emp = CsvStream(
            EmployeeHeader,
            "new-emp,New Person,new@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "existing-emp,,3CC 3333,car,false,true");

        var (result, error) = await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(1, result!.VehiclesApplied);

        var hash = EmployeeBootstrapService.Hash("existing-emp");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        Assert.Single(profile!.Vehicles);
    }

    // ── Vehicle validation errors ─────────────────────────────────────────────

    [Fact]
    public async Task Preview_UnknownVehicleSubject_RejectsVehicleRow()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "unknown-emp,,4DD 4444,car,false,true");

        var (preview, error) = await service.PreviewAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(0, preview!.VehiclesValid);
        Assert.Equal(1, preview.VehiclesRejected);
        Assert.Contains("does not match", preview.VehicleRows[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Commit_UnknownVehicleSubject_RejectsWholeCommit()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "nobody,,5EE 5555,car,false,true");

        var (result, error) = await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(0, result!.Applied);        // employees not applied
        Assert.Equal(0, result.VehiclesApplied);
        Assert.Equal(1, result.VehiclesRejected);

        // Employee was not persisted since vehicle errors blocked commit.
        var hash = EmployeeBootstrapService.Hash("emp1");
        Assert.Null(await profileRepo.GetAsync("t1", hash, CancellationToken.None));
    }

    [Fact]
    public async Task Preview_DuplicatePlateInFile_RejectsSecondRow()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true",
            "emp2,Bob,bob@c.com,employee,Prague,,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,,6FF 6666,car,false,true",
            "emp2,,6FF 6666,car,false,true"); // same plate

        var (preview, error) = await service.PreviewAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(1, preview!.VehiclesValid);
        Assert.Equal(1, preview.VehiclesRejected);
        Assert.Contains("Duplicate", preview.VehicleRows[1].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_InvalidVehicleType_RejectsRow()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,,7GG 7777,truck,false,true"); // invalid type

        var (preview, error) = await service.PreviewAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(0, preview!.VehiclesValid);
        Assert.Equal(1, preview.VehiclesRejected);
        Assert.Contains("truck", preview.VehicleRows[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_InactiveVehicle_IsClassifiedAsValid()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,,8HH 8888,car,false,false"); // active=false

        var (preview, error) = await service.PreviewAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(1, preview!.VehiclesValid);
        Assert.Equal(0, preview.VehiclesRejected);
    }

    [Fact]
    public async Task Commit_InactiveVehicle_IsStoredWithIsActiveFalse()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,,8HH 8888,car,false,false");

        var (result, error) = await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(1, result!.VehiclesApplied);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        var vehicle = Assert.Single(profile!.Vehicles);
        Assert.False(vehicle.IsActive);
    }

    // ── Idempotent re-import ──────────────────────────────────────────────────

    [Fact]
    public async Task Commit_IdempotentReImport_DoesNotCreateDuplicateVehicle()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,,9II 9999,car,false,true");

        // First import.
        await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        // Second import with the same data (re-use streams).
        var emp2 = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh2 = CsvStream(
            VehicleHeader,
            "emp1,,9II 9999,car,false,true");

        var (result, error) = await service.CommitAsync("t1", emp2, veh2, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        // Idempotent: exactly one vehicle, not two.
        Assert.Single(profile!.Vehicles);
    }

    // ── Vehicle alias ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Commit_VehicleWithAlias_StoresAlias()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,My Blue Car,0JJ 0000,car,false,true");

        await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        var vehicle = Assert.Single(profile!.Vehicles);
        Assert.Equal("My Blue Car", vehicle.Alias);
    }

    // ── Multiple vehicles per profile ─────────────────────────────────────────

    [Fact]
    public async Task Commit_MultipleVehiclesForSameEmployee_AllPersisted()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,Car A,AA 1111,car,false,true",
            "emp1,Car B,BB 2222,car,true,true");

        var (result, error) = await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(2, result!.VehiclesApplied);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        Assert.Equal(2, profile!.Vehicles.Count);
    }

    // ── Employee errors block commit ───────────────────────────────────────────

    [Fact]
    public async Task Commit_EmployeeRowHasError_VehiclesNotApplied()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true",
            "emp2,Bob,bob@c.com,overlord,Prague,,true,false,false,false,true"); // invalid role
        var veh = CsvStream(
            VehicleHeader,
            "emp1,,CC 3333,car,false,true");

        var (result, error) = await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(0, result!.Applied);
        Assert.Equal(1, result.Rejected);
        Assert.Equal(0, result.VehiclesApplied);

        // Neither employee nor vehicle was persisted.
        Assert.Null(await profileRepo.GetAsync("t1", EmployeeBootstrapService.Hash("emp1"), CancellationToken.None));
    }

    // ── Vehicle CSV header validation ─────────────────────────────────────────

    [Fact]
    public async Task Preview_VehicleCsvUnknownColumn_ReturnsError()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            "external_subject,vehicle_license_plate,vehicle_type,vehicle_is_electric,active,secret_field");

        var (preview, error) = await service.PreviewAsync("t1", emp, veh, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Null(preview);
        Assert.Contains("secret_field", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_VehicleCsvEmpty_ReturnsError()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream("# just a comment");

        var (preview, error) = await service.PreviewAsync("t1", emp, veh, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Null(preview);
    }

    // ── First active vehicle is set as default ─────────────────────────────────

    [Fact]
    public async Task Commit_FirstActiveVehicle_IsSetAsDefault()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,,DD 4444,car,false,true");

        await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        var vehicle = Assert.Single(profile!.Vehicles);
        Assert.True(vehicle.IsDefault);
    }

    // ── Electric vehicle ──────────────────────────────────────────────────────

    [Fact]
    public async Task Commit_ElectricVehicle_StoresIsElectricTrue()
    {
        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,EV,EE 5555,car,true,true");

        await service.CommitAsync("t1", emp, veh, CancellationToken.None);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        var vehicle = Assert.Single(profile!.Vehicles);
        Assert.True(vehicle.IsElectric);
    }

    // ── Service-level employee error stops vehicle apply ───────────────────────

    [Fact]
    public async Task Commit_EmployeeServiceError_VehiclesNotApplied()
    {
        // Pre-register emp1 so ClassifyAllAsync marks it as Updated.
        var inner = new InMemoryProfileRepository();
        var deactivated = new InMemoryDeactivatedUserStore();
        var bootstrapService = new EmployeeBootstrapService(inner, deactivated);
        await bootstrapService.RegisterAsync("t1", new BootstrapEmployeeRequest(
            "emp1", null, true, ["employee"], null, null, true, false, false, false, "seed"),
            CancellationToken.None);

        // The 2nd GetAsync for emp1 (inside UpdateAsync) returns null, simulating a
        // race-condition delete between classify and apply.
        var emp1Hash = EmployeeBootstrapService.Hash("emp1");
        var failingRepo = new NthCallNullRepository(inner, emp1Hash, failOnCallN: 2);
        var svc = new HrImportService(
            failingRepo, deactivated, new StaticCurrentUser("actor-1", "t1"),
            NullLogger<HrImportService>.Instance);

        var emp = CsvStream(
            EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh = CsvStream(
            VehicleHeader,
            "emp1,,AA 0001,car,false,true");

        var (result, error) = await svc.CommitAsync("t1", emp, veh, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        // Employee apply failed — service returned an error string.
        Assert.Equal(1, result!.Rejected);
        // Vehicles must not be applied when any employee had a service error.
        Assert.Equal(0, result.VehiclesApplied);
    }

    // ── Re-import updates all vehicle facts ────────────────────────────────────

    [Fact]
    public async Task Commit_ReImportWithChangedFacts_UpdatesTypeElectricAndAlias()
    {
        // First import: car, non-electric, alias "OldName".
        var emp1 = CsvStream(EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh1 = CsvStream(VehicleHeader, "emp1,OldName,VV 0001,car,false,true");
        await service.CommitAsync("t1", emp1, veh1, CancellationToken.None);

        // Second import: same plate, but van + electric + updated alias.
        var emp2 = CsvStream(EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh2 = CsvStream(VehicleHeader, "emp1,NewName,VV 0001,van,true,true");
        var (result, error) = await service.CommitAsync("t1", emp2, veh2, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(1, result!.VehiclesApplied);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        var vehicle = Assert.Single(profile!.Vehicles);
        Assert.Equal("van", vehicle.VehicleType);
        Assert.True(vehicle.IsElectric);
        Assert.Equal("NewName", vehicle.Alias);
    }

    // ── Vehicle default normalization ─────────────────────────────────────────

    [Fact]
    public async Task Commit_DeactivatingDefaultVehicle_ClearsDefault()
    {
        // First import: active vehicle — becomes default.
        var emp1 = CsvStream(EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh1 = CsvStream(VehicleHeader, "emp1,,AA 1111,car,false,true");
        await service.CommitAsync("t1", emp1, veh1, CancellationToken.None);

        // Second import: deactivate the same vehicle.
        var emp2 = CsvStream(EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh2 = CsvStream(VehicleHeader, "emp1,,AA 1111,car,false,false");
        await service.CommitAsync("t1", emp2, veh2, CancellationToken.None);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        var vehicle = Assert.Single(profile!.Vehicles);
        Assert.False(vehicle.IsActive);
        Assert.False(vehicle.IsDefault);
    }

    [Fact]
    public async Task Commit_ReactivatingOnlyActiveVehicle_BecomesDefault()
    {
        // First import: active vehicle (becomes default).
        var emp1 = CsvStream(EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh1 = CsvStream(VehicleHeader, "emp1,,BB 2222,car,false,true");
        await service.CommitAsync("t1", emp1, veh1, CancellationToken.None);

        // Second import: deactivate it (default cleared).
        var emp2 = CsvStream(EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh2 = CsvStream(VehicleHeader, "emp1,,BB 2222,car,false,false");
        await service.CommitAsync("t1", emp2, veh2, CancellationToken.None);

        // Third import: reactivate it — must be promoted to default.
        var emp3 = CsvStream(EmployeeHeader,
            "emp1,Alice,alice@c.com,employee,Prague,A,true,false,false,false,true");
        var veh3 = CsvStream(VehicleHeader, "emp1,,BB 2222,car,false,true");
        await service.CommitAsync("t1", emp3, veh3, CancellationToken.None);

        var hash = EmployeeBootstrapService.Hash("emp1");
        var profile = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        var vehicle = Assert.Single(profile!.Vehicles);
        Assert.True(vehicle.IsActive);
        Assert.True(vehicle.IsDefault);
    }
}

/// <summary>Minimal ICurrentUser for unit tests.</summary>
file sealed class StaticCurrentUser(string userId, string tenantId) : ICurrentUser
{
    public bool IsAuthenticated => true;
    public string UserId => userId;
    public string TenantId => tenantId;
    public IReadOnlyList<string> Roles => [];
    public bool IsInRole(string role) => false;
}

/// <summary>
/// Wraps an <see cref="InMemoryProfileRepository"/> and returns <c>null</c> on the
/// Nth <see cref="GetAsync"/> call for a specific user, simulating a race-condition
/// profile deletion between the classify and apply phases.
/// </summary>
file sealed class NthCallNullRepository(
    InMemoryProfileRepository inner,
    string failUserId,
    int failOnCallN) : IProfileRepository
{
    private int _callCount;

    public Task<UserProfile?> GetAsync(string tenantId, string userId, CancellationToken ct)
    {
        if (string.Equals(userId, failUserId, StringComparison.Ordinal))
        {
            var count = Interlocked.Increment(ref _callCount);
            if (count == failOnCallN)
                return Task.FromResult<UserProfile?>(null);
        }
        return inner.GetAsync(tenantId, userId, ct);
    }

    public Task<bool> EmployeeIdExistsAsync(string tenantId, string employeeId, CancellationToken ct) =>
        inner.EmployeeIdExistsAsync(tenantId, employeeId, ct);

    public Task SaveAsync(UserProfile profile, CancellationToken ct) =>
        inner.SaveAsync(profile, ct);
}
