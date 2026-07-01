using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Infrastructure.Repositories;

public sealed class DaprCorrectionAuditRepository : ICorrectionAuditRepository
{
    private readonly DaprClient daprClient;
    private const string BookingStore = "bookingstore";

    public DaprCorrectionAuditRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task SaveAsync(CorrectionAuditDto audit, CancellationToken cancellationToken = default)
    {
        // Each correction is stored with a unique key to preserve append-only semantics.
        var key = TenantStorageKey.For(
            "correction", audit.TenantId,
            $"{audit.RequestId}:{audit.AppliedAt:yyyyMMddHHmmssfff}:{audit.Id}");
        await daprClient.SaveStateAsync(BookingStore, key, audit, cancellationToken: cancellationToken);

        // Record the key in the per-tenant index so a destructive tenant purge (PLAT003C)
        // reaches every correction audit — these records have no other index.
        await AppendToCorrectionIndexAsync(audit.TenantId, key, cancellationToken);
    }

    public async Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var indexKey = CorrectionIndexKey(tenantId);
        var correctionKeys = await daprClient.GetStateAsync<List<string>>(
            BookingStore, indexKey, cancellationToken: cancellationToken) ?? [];

        var removed = 0;
        foreach (var key in correctionKeys)
        {
            await daprClient.DeleteStateAsync(BookingStore, key, cancellationToken: cancellationToken);
            removed++;
        }

        // Remove the index last so a re-run finds nothing, enumerates nothing, and returns 0.
        await daprClient.DeleteStateAsync(BookingStore, indexKey, cancellationToken: cancellationToken);
        return removed;
    }

    private static string CorrectionIndexKey(string tenantId)
        => TenantStorageKey.For("correction-index", tenantId, "all");

    private async Task AppendToCorrectionIndexAsync(string tenantId, string correctionKey, CancellationToken cancellationToken)
    {
        var indexKey = CorrectionIndexKey(tenantId);
        var index = await daprClient.GetStateAsync<List<string>>(
            BookingStore, indexKey, cancellationToken: cancellationToken) ?? [];

        if (index.Contains(correctionKey))
            return;

        index.Add(correctionKey);
        await daprClient.SaveStateAsync(BookingStore, indexKey, index, cancellationToken: cancellationToken);
    }
}
