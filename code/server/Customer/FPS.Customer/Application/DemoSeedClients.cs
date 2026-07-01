using System.Net.Http;
using Dapr.Client;

namespace FPS.Customer.Application;

// PLAT003C-C2: the demo-seed reseed calls Profile/Configuration over Dapr service invocation so the
// receiving endpoints can enforce [DaprInternalOnly] (dapr-api-token) — a real internal boundary the
// gateway can't reach — instead of a key-only anonymous HTTP endpoint. The tenant id travels in the
// body because a scheduled reset has no operator JWT. authorizationHeader is retained on the contract
// for the operator-initiated seed path but is NOT used for transport auth (Dapr injects the token).
public interface IDemoSeedProfileClient
{
    Task<(int profilesSeeded, string? error)> SeedAsync(
        string authorizationHeader,
        string tenantId,
        IReadOnlyList<DemoEmployeeRecord> employees,
        CancellationToken ct);
}

public interface IDemoSeedConfigurationClient
{
    Task<(int slotsSeeded, string? error)> SeedAsync(
        string authorizationHeader,
        string tenantId,
        string locationId,
        IReadOnlyList<DemoSlotRecord> slots,
        DemoPolicyRecord policy,
        CancellationToken ct);
}

public sealed class DaprDemoSeedProfileClient(DaprClient dapr) : IDemoSeedProfileClient
{
    public async Task<(int profilesSeeded, string? error)> SeedAsync(
        string authorizationHeader,
        string tenantId,
        IReadOnlyList<DemoEmployeeRecord> employees,
        CancellationToken ct)
    {
        try
        {
            await dapr.InvokeMethodAsync(
                "fps-profile", "profile/admin/demo-seed", new { tenantId, employees }, ct);
            return (employees.Count, null);
        }
        catch (Exception ex) when (ex is InvocationException or HttpRequestException or TaskCanceledException)
        {
            return (0, $"Profile seed failed: {ex.Message}");
        }
    }
}

public sealed class DaprDemoSeedConfigurationClient(DaprClient dapr) : IDemoSeedConfigurationClient
{
    public async Task<(int slotsSeeded, string? error)> SeedAsync(
        string authorizationHeader,
        string tenantId,
        string locationId,
        IReadOnlyList<DemoSlotRecord> slots,
        DemoPolicyRecord policy,
        CancellationToken ct)
    {
        try
        {
            await dapr.InvokeMethodAsync(
                "fps-configuration", "configuration/admin/demo-seed",
                new { tenantId, locationId, slots, policy }, ct);
            return (slots.Count, null);
        }
        catch (Exception ex) when (ex is InvocationException or HttpRequestException or TaskCanceledException)
        {
            return (0, $"Configuration seed failed: {ex.Message}");
        }
    }
}
