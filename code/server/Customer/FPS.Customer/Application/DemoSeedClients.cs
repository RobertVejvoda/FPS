using System.Net.Http;
using System.Net.Http.Json;
using Dapr.Client;

namespace FPS.Customer.Application;

// PLAT003C-C2: the demo-seed reseed calls Profile/Configuration over Dapr service invocation (via a
// Dapr invoke HttpClient) so the receiving endpoints can enforce [DaprInternalOnly] (dapr-api-token) —
// a real internal boundary the gateway can't reach — instead of a key-only anonymous HTTP endpoint.
// The tenant id travels in the body because a scheduled reset has no operator JWT. authorizationHeader
// is retained on the contract for the operator-initiated seed path but is NOT used for transport auth
// (the Dapr sidecar injects the token on invocation).
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

public sealed class DaprDemoSeedProfileClient : IDemoSeedProfileClient
{
    public async Task<(int profilesSeeded, string? error)> SeedAsync(
        string authorizationHeader,
        string tenantId,
        IReadOnlyList<DemoEmployeeRecord> employees,
        CancellationToken ct)
    {
        try
        {
            using var http = DaprClient.CreateInvokeHttpClient("fps-profile");
            using var response = await http.PostAsJsonAsync(
                "profile/admin/demo-seed", new { tenantId, employees }, ct);
            return response.IsSuccessStatusCode
                ? (employees.Count, null)
                : (0, $"Profile seed failed: HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (0, $"Profile service unreachable: {ex.Message}");
        }
    }
}

public sealed class DaprDemoSeedConfigurationClient : IDemoSeedConfigurationClient
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
            using var http = DaprClient.CreateInvokeHttpClient("fps-configuration");
            using var response = await http.PostAsJsonAsync(
                "configuration/admin/demo-seed", new { tenantId, locationId, slots, policy }, ct);
            return response.IsSuccessStatusCode
                ? (slots.Count, null)
                : (0, $"Configuration seed failed: HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (0, $"Configuration service unreachable: {ex.Message}");
        }
    }
}
