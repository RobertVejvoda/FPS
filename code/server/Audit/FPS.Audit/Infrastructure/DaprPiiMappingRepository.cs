using Dapr.Client;
using FPS.Audit.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Audit.Infrastructure;

public sealed class DaprPiiMappingRepository : IPiiMappingRepository
{
    private readonly DaprClient daprClient;
    private const string StoreName = "pii-mappingstore";

    public DaprPiiMappingRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task SaveAsync(PiiMapping mapping, CancellationToken ct = default)
    {
        await daprClient.SaveStateAsync(StoreName, MappingKey(mapping.TenantId, mapping.UserId), mapping, cancellationToken: ct);
        await daprClient.SaveStateAsync(StoreName, HashIndexKey(mapping.TenantId, mapping.ActorHash), mapping.UserId, cancellationToken: ct);
    }

    public async Task DeleteByUserIdAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        var mapping = await daprClient.GetStateAsync<PiiMapping>(StoreName, MappingKey(tenantId, userId), cancellationToken: ct);
        if (mapping is not null)
            await daprClient.DeleteStateAsync(StoreName, HashIndexKey(tenantId, mapping.ActorHash), cancellationToken: ct);
        await daprClient.DeleteStateAsync(StoreName, MappingKey(tenantId, userId), cancellationToken: ct);
    }

    public async Task DeleteByActorHashAsync(string actorHash, string tenantId, CancellationToken ct = default)
    {
        var userId = await daprClient.GetStateAsync<string>(StoreName, HashIndexKey(tenantId, actorHash), cancellationToken: ct);
        if (userId is not null)
            await daprClient.DeleteStateAsync(StoreName, MappingKey(tenantId, userId), cancellationToken: ct);
        await daprClient.DeleteStateAsync(StoreName, HashIndexKey(tenantId, actorHash), cancellationToken: ct);
    }

    public async Task<bool> ExistsAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        var mapping = await daprClient.GetStateAsync<PiiMapping>(StoreName, MappingKey(tenantId, userId), cancellationToken: ct);
        return mapping is not null;
    }

    public async Task<IReadOnlyDictionary<string, PiiMapping>> GetByActorHashesAsync(
        string tenantId, IReadOnlyList<string> actorHashes, CancellationToken ct = default)
    {
        var result = new Dictionary<string, PiiMapping>(StringComparer.OrdinalIgnoreCase);
        foreach (var hash in actorHashes)
        {
            var userId = await daprClient.GetStateAsync<string>(StoreName, HashIndexKey(tenantId, hash), cancellationToken: ct);
            if (userId is null) continue;
            var mapping = await daprClient.GetStateAsync<PiiMapping>(StoreName, MappingKey(tenantId, userId), cancellationToken: ct);
            if (mapping is not null) result[hash] = mapping;
        }
        return result;
    }

    private static string MappingKey(string tenantId, string userId) => TenantStorageKey.For("pii", tenantId, userId);
    private static string HashIndexKey(string tenantId, string actorHash) => TenantStorageKey.For("pii-hash", tenantId, actorHash);
}
