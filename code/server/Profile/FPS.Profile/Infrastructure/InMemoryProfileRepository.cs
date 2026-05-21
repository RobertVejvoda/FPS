using FPS.Profile.Application;
using FPS.Profile.Domain;
using System.Collections.Concurrent;

namespace FPS.Profile.Infrastructure;

// Phase 1 stub — replace with Dapr state store / MongoDB.
public sealed class InMemoryProfileRepository : IProfileRepository
{
    private readonly ConcurrentDictionary<string, UserProfile> store = new();

    public Task<UserProfile?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        store.TryGetValue(Key(tenantId, userId), out var profile);
        return Task.FromResult(profile);
    }

    public Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        store[Key(profile.TenantId, profile.UserId)] = profile;
        return Task.CompletedTask;
    }

    private static string Key(string tenantId, string userId) => $"{tenantId}:{userId}";
}
