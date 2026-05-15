using FPS.Audit.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Audit.Application;

public sealed class PiiErasureService(IPiiMappingRepository repository, ILogger<PiiErasureService> logger)
{
    public async Task DeleteByUserIdAsync(
        string userId, string tenantId, string requestorActorHash, CancellationToken cancellationToken = default)
    {
        await repository.DeleteByUserIdAsync(userId, tenantId, cancellationToken);

        // Log with hashed references only — raw userId must not appear in logs.
        logger.LogInformation(
            "[PII-Erasure] HashedUserId={HashedUserId} TenantId={TenantId} RequestedByHash={RequestorHash}",
            Pseudonymiser.Hash(userId), tenantId, requestorActorHash);
    }
}
