using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FPS.DataHub.Infrastructure;

public sealed class EventInboxHealthCheck(DataHubDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var poisonCount = await db.EventInbox
            .CountAsync(e => e.ProcessingStatus == EventProcessingStatus.Poisoned, ct);
        var failedCount = await db.EventInbox
            .CountAsync(e => e.ProcessingStatus == EventProcessingStatus.Failed, ct);
        var pendingCount = await db.EventInbox
            .CountAsync(e => e.ProcessingStatus == EventProcessingStatus.Pending, ct);

        var data = new Dictionary<string, object>
        {
            ["poison_events"] = poisonCount,
            ["failed_events"] = failedCount,
            ["pending_events"] = pendingCount,
        };

        if (poisonCount > 0)
            return HealthCheckResult.Degraded($"{poisonCount} poison event(s) in inbox.", null, data);

        return HealthCheckResult.Healthy("Event inbox healthy.", data);
    }
}
