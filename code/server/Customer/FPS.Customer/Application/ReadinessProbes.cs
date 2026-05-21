namespace FPS.Customer.Application;

// Out-of-process readiness probes. In-process (local) checks live in TenantReadinessService.
// Each probe returns a ReadinessCheckResult. Stubs skip with an explanatory reason so the
// report is honest about what was and was not verified.

public interface IProfileReadinessProbe
{
    Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct);
}

public interface IBookingReadinessProbe
{
    Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct);
}

public interface INotificationReadinessProbe
{
    Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct);
}

public interface IAuditReadinessProbe
{
    Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct);
}

public interface IReportingReadinessProbe
{
    Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct);
}

// No-op stubs — replaced with real HTTP/Dapr probes in later phases.
public sealed class NoOpProfileReadinessProbe : IProfileReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Skipped("ProfileFacts",
            "Profile service probe not connected in this deployment."));
}

public sealed class NoOpBookingReadinessProbe : IBookingReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Skipped("BookingSmokeTest",
            "Booking service probe not connected in this deployment."));
}

public sealed class NoOpNotificationReadinessProbe : INotificationReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Skipped("NotificationReachable",
            "Notification service probe not connected in this deployment."));
}

public sealed class NoOpAuditReadinessProbe : IAuditReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Skipped("AuditEvidence",
            "Audit service probe not connected in this deployment."));
}

public sealed class NoOpReportingReadinessProbe : IReportingReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Skipped("ReportingEvidence",
            "Reporting service probe not connected in this deployment."));
}
