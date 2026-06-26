using Dapr.Client;
using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Profile.Infrastructure;

public sealed class DaprProfileRepository : IProfileRepository
{
    private readonly DaprClient daprClient;
    private const string StoreName = "profilestore";

    public DaprProfileRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<UserProfile?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
        => await daprClient.GetStateAsync<UserProfile>(StoreName, ProfileKey(tenantId, userId), cancellationToken: cancellationToken);

    public async Task<bool> EmployeeIdExistsAsync(string tenantId, string employeeId, CancellationToken cancellationToken = default)
        => await daprClient.GetStateAsync<bool>(StoreName, EmpIdKey(tenantId, employeeId), cancellationToken: cancellationToken);

    public async Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        await daprClient.SaveStateAsync(StoreName, ProfileKey(profile.TenantId, profile.UserId), profile, cancellationToken: cancellationToken);

        if (profile.EmployeeId is not null)
            await daprClient.SaveStateAsync(StoreName, EmpIdKey(profile.TenantId, profile.EmployeeId), true, cancellationToken: cancellationToken);

        await AddToTenantIndexAsync(profile.TenantId, profile.UserId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserProfile>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var userIds = await daprClient.GetStateAsync<List<string>>(
                          StoreName, TenantIndexKey(tenantId), cancellationToken: cancellationToken)
                      ?? [];

        var results = new List<UserProfile>(userIds.Count);
        foreach (var userId in userIds)
        {
            var profile = await daprClient.GetStateAsync<UserProfile>(
                StoreName, ProfileKey(tenantId, userId), cancellationToken: cancellationToken);
            if (profile is not null)
                results.Add(profile);
        }
        return results;
    }

    private async Task AddToTenantIndexAsync(string tenantId, string userId, CancellationToken cancellationToken)
    {
        var key = TenantIndexKey(tenantId);
        var index = await daprClient.GetStateAsync<List<string>>(StoreName, key, cancellationToken: cancellationToken) ?? [];
        if (!index.Contains(userId, StringComparer.Ordinal))
        {
            index.Add(userId);
            await daprClient.SaveStateAsync(StoreName, key, index, cancellationToken: cancellationToken);
        }
    }

    // Key shapes:
    //   profile:{tenantId}:{userId}           — full UserProfile document (includes vehicles)
    //   profile-empidx:{tenantId}:{employeeId} — bool flag for employee ID uniqueness check
    //   profile-index:{tenantId}:all           — List<string> of userIds for tenant enumeration

    private static string ProfileKey(string tenantId, string userId)
        => TenantStorageKey.For("profile", tenantId, userId);

    private static string EmpIdKey(string tenantId, string employeeId)
        => TenantStorageKey.For("profile-empidx", tenantId, employeeId);

    private static string TenantIndexKey(string tenantId)
        => TenantStorageKey.For("profile-index", tenantId, "all");
}
