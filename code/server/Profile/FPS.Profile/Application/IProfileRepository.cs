using FPS.Profile.Domain;

namespace FPS.Profile.Application;

public interface IProfileRepository
{
    Task<UserProfile?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default);
}
