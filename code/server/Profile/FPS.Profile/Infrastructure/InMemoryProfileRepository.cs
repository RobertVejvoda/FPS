using FPS.Profile.Application;
using FPS.Profile.Domain;
using System.Collections.Concurrent;

namespace FPS.Profile.Infrastructure;

// Phase 1 stub — replace with Dapr state store / MongoDB.
public sealed class InMemoryProfileRepository : IProfileRepository
{
    private readonly ConcurrentDictionary<string, UserProfile> store = new();
    // Secondary index: "{tenantId}:{employeeId}" → true, for fast duplicate detection.
    private readonly ConcurrentDictionary<string, bool> employeeIdIndex = new(StringComparer.OrdinalIgnoreCase);

    public Task<UserProfile?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        store.TryGetValue(ProfileKey(tenantId, userId), out var profile);
        return Task.FromResult(profile);
    }

    public Task<bool> EmployeeIdExistsAsync(string tenantId, string employeeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(employeeIdIndex.ContainsKey(EmpKey(tenantId, employeeId)));

    public Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        store[ProfileKey(profile.TenantId, profile.UserId)] = profile;
        if (profile.EmployeeId is not null)
            employeeIdIndex[EmpKey(profile.TenantId, profile.EmployeeId)] = true;
        return Task.CompletedTask;
    }

    private static string ProfileKey(string tenantId, string userId) => $"{tenantId}:{userId}";
    private static string EmpKey(string tenantId, string employeeId) => $"{tenantId}:{employeeId}";
}
