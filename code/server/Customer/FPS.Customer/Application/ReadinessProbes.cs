namespace FPS.Customer.Application;

using Microsoft.Extensions.Configuration;

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

public abstract class HttpServiceReadinessProbe(HttpClient http, IConfiguration config)
{
    protected abstract string Name { get; }
    protected abstract string ConfigKey { get; }
    protected abstract string DefaultBaseUrl { get; }

    protected async Task<ReadinessCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        var baseUrl = config[ConfigKey] ?? DefaultBaseUrl;
        var healthUrl = $"{baseUrl.TrimEnd('/')}/health";
        try
        {
            using var response = await http.GetAsync(healthUrl, ct);
            return response.IsSuccessStatusCode
                ? ReadinessCheckResult.Pass(Name)
                : ReadinessCheckResult.Fail(Name, $"{healthUrl} returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ReadinessCheckResult.Fail(Name, $"{healthUrl} is not reachable: {ex.Message}");
        }
    }
}

public sealed class HttpProfileReadinessProbe(HttpClient http, IConfiguration config)
    : HttpServiceReadinessProbe(http, config), IProfileReadinessProbe
{
    protected override string Name => "ProfileFacts";
    protected override string ConfigKey => "Readiness:ProfileBaseUrl";
    protected override string DefaultBaseUrl => "http://localhost:5197";

    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        CheckHealthAsync(ct);
}

public sealed class HttpBookingReadinessProbe(HttpClient http, IConfiguration config)
    : HttpServiceReadinessProbe(http, config), IBookingReadinessProbe
{
    protected override string Name => "BookingSmokeTest";
    protected override string ConfigKey => "Readiness:BookingBaseUrl";
    protected override string DefaultBaseUrl => "http://localhost:5131";

    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        CheckHealthAsync(ct);
}

public sealed class HttpNotificationReadinessProbe(HttpClient http, IConfiguration config)
    : HttpServiceReadinessProbe(http, config), INotificationReadinessProbe
{
    protected override string Name => "NotificationReachable";
    protected override string ConfigKey => "Readiness:NotificationBaseUrl";
    protected override string DefaultBaseUrl => "http://localhost:5157";

    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        CheckHealthAsync(ct);
}

public sealed class HttpAuditReadinessProbe(HttpClient http, IConfiguration config)
    : HttpServiceReadinessProbe(http, config), IAuditReadinessProbe
{
    protected override string Name => "AuditEvidence";
    protected override string ConfigKey => "Readiness:AuditBaseUrl";
    protected override string DefaultBaseUrl => "http://localhost:5161";

    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        CheckHealthAsync(ct);
}

public sealed class HttpReportingReadinessProbe(HttpClient http, IConfiguration config)
    : HttpServiceReadinessProbe(http, config), IReportingReadinessProbe
{
    protected override string Name => "ReportingEvidence";
    protected override string ConfigKey => "Readiness:ReportingBaseUrl";
    protected override string DefaultBaseUrl => "http://localhost:5171";

    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        CheckHealthAsync(ct);
}

// No-op stubs — replaced with real HTTP/Dapr probes in later phases.
// These return Failed because the checks are required for live readiness;
// a deployment without connected probes cannot be marked Ready.
public sealed class NoOpProfileReadinessProbe : IProfileReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Fail("ProfileFacts",
            "Profile service probe not connected. Connect probe before marking tenant Ready."));
}

public sealed class NoOpBookingReadinessProbe : IBookingReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Fail("BookingSmokeTest",
            "Booking service probe not connected. Connect probe before marking tenant Ready."));
}

public sealed class NoOpNotificationReadinessProbe : INotificationReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Fail("NotificationReachable",
            "Notification service probe not connected. Connect probe before marking tenant Ready."));
}

public sealed class NoOpAuditReadinessProbe : IAuditReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Fail("AuditEvidence",
            "Audit service probe not connected. Connect probe before marking tenant Ready."));
}

public sealed class NoOpReportingReadinessProbe : IReportingReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(ReadinessCheckResult.Fail("ReportingEvidence",
            "Reporting service probe not connected. Connect probe before marking tenant Ready."));
}
