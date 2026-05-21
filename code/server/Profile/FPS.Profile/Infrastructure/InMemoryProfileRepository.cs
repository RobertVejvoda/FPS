using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.SharedKernel.Profile;
using System.Collections.Concurrent;

namespace FPS.Profile.Infrastructure;

// Phase 1 stub — replace with Dapr state store / MongoDB.
public sealed class InMemoryProfileRepository : IProfileRepository, IProfileBootstrapSink
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

    public Task UpsertAsync(
        string tenantId, string subjectHash, bool isActive,
        bool parkingEligible, bool hasCompanyCar, bool accessibilityEligible, bool reservedSpaceEligible,
        string factSource, CancellationToken ct)
    {
        var profile = new UserProfile
        {
            TenantId = tenantId,
            UserId = subjectHash,
            Status = isActive ? ProfileStatus.Active : ProfileStatus.Inactive,
            ParkingEligible = parkingEligible,
            HasCompanyCar = hasCompanyCar,
            AccessibilityEligible = accessibilityEligible,
            ReservedSpaceEligible = reservedSpaceEligible,
            Vehicles = [],
            SnapshotVersion = Guid.NewGuid().ToString(),
            UpdatedAt = DateTimeOffset.UtcNow,
            FactSource = factSource,
        };
        store[Key(tenantId, subjectHash)] = profile;
        return Task.CompletedTask;
    }

    private static string Key(string tenantId, string userId) => $"{tenantId}:{userId}";
}
