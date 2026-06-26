using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// Reports Unhealthy when identity store hydration failed at startup.
/// An Unhealthy result causes the readiness probe to block traffic, preventing
/// the service from serving requests with empty (fail-open) identity stores.
/// </summary>
public sealed class IdentityHydrationHealthCheck : IHealthCheck
{
    private bool hydrationFailed;

    public void MarkFailed() => hydrationFailed = true;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(hydrationFailed
            ? HealthCheckResult.Unhealthy(
                "Identity store hydration failed at startup. Stores are empty and enforcement is disabled — " +
                "traffic is blocked until the service restarts with Dapr available.")
            : HealthCheckResult.Healthy("Identity stores hydrated successfully."));
}
