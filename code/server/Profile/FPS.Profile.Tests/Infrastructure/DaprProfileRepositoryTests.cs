using Dapr.Client;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using Moq;

namespace FPS.Profile.Tests.Infrastructure;

/// <summary>
/// Tests DaprProfileRepository using a mocked DaprClient backed by a shared
/// in-process dictionary, proving cold-restart persistence semantics and tenant isolation.
/// </summary>
public sealed class DaprProfileRepositoryTests
{
    private const string StoreName = "profilestore";

    private readonly Dictionary<string, object?> store = new();

    private DaprProfileRepository BuildRepo()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(
                StoreName, It.IsAny<string>(), It.IsAny<UserProfile>(),
                null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, UserProfile, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(
                StoreName, It.IsAny<string>(), It.IsAny<bool>(),
                null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(
                StoreName, It.IsAny<string>(), It.IsAny<List<string>>(),
                null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<string>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<UserProfile>(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var val) ? val as UserProfile : null);

        mock.Setup(c => c.GetStateAsync<bool>(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var val) && val is bool b && b);

        mock.Setup(c => c.GetStateAsync<List<string>>(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var val) ? val as List<string> : null);

        mock.Setup(c => c.DeleteStateAsync(
                StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, _, _, _) => store.Remove(key))
            .Returns(Task.CompletedTask);

        return new DaprProfileRepository(mock.Object);
    }

    private static UserProfile MakeProfile(string tenantId, string userId, bool eligible = true, string? employeeId = null) =>
        new()
        {
            TenantId = tenantId,
            UserId = userId,
            Status = ProfileStatus.Active,
            ParkingEligible = eligible,
            EmployeeId = employeeId,
            SnapshotVersion = Guid.NewGuid().ToString(),
            FactSource = "test",
            Vehicles =
            [
                new Vehicle("v1", "ABC-001", "Sedan", false, true, IsDefault: true)
            ],
        };

    // ── Cold-restart persistence: new repo instance reads same backing store ──

    [Fact]
    public async Task GetAsync_AfterSave_ReturnsProfile()
    {
        var repo1 = BuildRepo();
        await repo1.SaveAsync(MakeProfile("demo", "user-1", eligible: true));

        var repo2 = BuildRepo(); // simulates restart
        var result = await repo2.GetAsync("demo", "user-1");

        Assert.NotNull(result);
        Assert.True(result!.ParkingEligible);
    }

    [Fact]
    public async Task GetAsync_VehicleFacts_SurviveRestart()
    {
        var repo1 = BuildRepo();
        await repo1.SaveAsync(MakeProfile("demo", "user-2"));

        var repo2 = BuildRepo();
        var result = await repo2.GetAsync("demo", "user-2");

        Assert.NotNull(result);
        Assert.Single(result!.Vehicles);
        Assert.Equal("ABC-001", result.Vehicles[0].LicensePlate);
        Assert.True(result.Vehicles[0].IsDefault);
    }

    [Fact]
    public async Task GetAsync_UnknownUser_ReturnsNull()
    {
        var repo = BuildRepo();
        Assert.Null(await repo.GetAsync("demo", "unknown-user"));
    }

    // ── EmployeeId index survives restart ─────────────────────────────────────

    [Fact]
    public async Task EmployeeIdExistsAsync_AfterSave_ReturnsTrue()
    {
        var repo1 = BuildRepo();
        await repo1.SaveAsync(MakeProfile("demo", "user-1", employeeId: "EMP-001"));

        var repo2 = BuildRepo();
        Assert.True(await repo2.EmployeeIdExistsAsync("demo", "EMP-001"));
    }

    [Fact]
    public async Task EmployeeIdExistsAsync_NoProfile_ReturnsFalse()
    {
        var repo = BuildRepo();
        Assert.False(await repo.EmployeeIdExistsAsync("demo", "EMP-999"));
    }

    [Fact]
    public async Task EmployeeIdExistsAsync_TenantIsolation_ReturnsFalseForOtherTenant()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeProfile("demo", "user-1", employeeId: "EMP-001"));

        Assert.False(await repo.EmployeeIdExistsAsync("other-co", "EMP-001"));
    }

    // ── ListByTenantAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListByTenantAsync_AfterSave_ReturnsProfiles()
    {
        var repo1 = BuildRepo();
        await repo1.SaveAsync(MakeProfile("demo", "user-1"));
        await repo1.SaveAsync(MakeProfile("demo", "user-2"));

        var repo2 = BuildRepo();
        var list = await repo2.ListByTenantAsync("demo");

        Assert.Equal(2, list.Count);
        Assert.Contains(list, p => p.UserId == "user-1");
        Assert.Contains(list, p => p.UserId == "user-2");
    }

    [Fact]
    public async Task ListByTenantAsync_TenantIsolation_OnlyOwnTenantProfiles()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeProfile("demo", "user-1"));
        await repo.SaveAsync(MakeProfile("other-co", "user-99"));

        var demoList = await repo.ListByTenantAsync("demo");
        Assert.Single(demoList);
        Assert.Equal("user-1", demoList[0].UserId);

        var otherList = await repo.ListByTenantAsync("other-co");
        Assert.Single(otherList);
        Assert.Equal("user-99", otherList[0].UserId);
    }

    [Fact]
    public async Task ListByTenantAsync_EmptyTenant_ReturnsEmpty()
    {
        var repo = BuildRepo();
        var list = await repo.ListByTenantAsync("demo");
        Assert.Empty(list);
    }

    // ── SaveAsync: save twice for same user → update, not duplicate ───────────

    [Fact]
    public async Task SaveAsync_UpdateProfile_OverwritesExisting()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeProfile("demo", "user-1", eligible: true));
        await repo.SaveAsync(MakeProfile("demo", "user-1", eligible: false));

        var result = await repo.GetAsync("demo", "user-1");
        Assert.False(result!.ParkingEligible);
    }

    [Fact]
    public async Task ListByTenantAsync_SaveSameUserTwice_CountRemainsOne()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeProfile("demo", "user-1"));
        await repo.SaveAsync(MakeProfile("demo", "user-1")); // update

        var list = await repo.ListByTenantAsync("demo");
        Assert.Single(list);
    }

    // ── PurgeTenantAsync (PLAT003C destructive purge) ─────────────────────────

    [Fact]
    public async Task PurgeTenantAsync_SeededTenant_RemovesAllProfilesAndReturnsCount()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeProfile("demo", "user-1", employeeId: "EMP-001"));
        await repo.SaveAsync(MakeProfile("demo", "user-2", employeeId: "EMP-002"));
        await repo.SaveAsync(MakeProfile("demo", "user-3"));

        var removed = await repo.PurgeTenantAsync("demo");

        Assert.Equal(3, removed);
        Assert.Empty(store); // every key (profiles, emp indices, tenant index) gone
        Assert.Empty(await repo.ListByTenantAsync("demo"));
        Assert.Null(await repo.GetAsync("demo", "user-1"));
        Assert.False(await repo.EmployeeIdExistsAsync("demo", "EMP-001"));
    }

    [Fact]
    public async Task PurgeTenantAsync_SecondCall_ReturnsZero()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeProfile("demo", "user-1"));
        await repo.SaveAsync(MakeProfile("demo", "user-2"));

        Assert.Equal(2, await repo.PurgeTenantAsync("demo"));
        Assert.Equal(0, await repo.PurgeTenantAsync("demo")); // idempotent
    }

    [Fact]
    public async Task PurgeTenantAsync_EmptyTenant_ReturnsZero()
    {
        var repo = BuildRepo();
        Assert.Equal(0, await repo.PurgeTenantAsync("demo"));
    }

    [Fact]
    public async Task PurgeTenantAsync_TenantIsolation_LeavesOtherTenantIntact()
    {
        var repo = BuildRepo();
        await repo.SaveAsync(MakeProfile("demo", "user-1", employeeId: "EMP-001"));
        await repo.SaveAsync(MakeProfile("other-co", "user-99", employeeId: "EMP-999"));

        var removed = await repo.PurgeTenantAsync("demo");

        Assert.Equal(1, removed);
        Assert.Empty(await repo.ListByTenantAsync("demo"));

        var otherList = await repo.ListByTenantAsync("other-co");
        Assert.Single(otherList);
        Assert.Equal("user-99", otherList[0].UserId);
        Assert.True(await repo.EmployeeIdExistsAsync("other-co", "EMP-999"));
    }
}
