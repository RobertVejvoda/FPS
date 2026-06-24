using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace FPS.Customer.Application;

public interface IDemoSeedProfileClient
{
    Task<(int profilesSeeded, string? error)> SeedAsync(
        string authorizationHeader,
        IReadOnlyList<DemoEmployeeRecord> employees,
        CancellationToken ct);
}

public interface IDemoSeedConfigurationClient
{
    Task<(int slotsSeeded, string? error)> SeedAsync(
        string authorizationHeader,
        string locationId,
        IReadOnlyList<DemoSlotRecord> slots,
        DemoPolicyRecord policy,
        CancellationToken ct);
}

public sealed class HttpDemoSeedProfileClient(HttpClient http, IConfiguration config) : IDemoSeedProfileClient
{
    public async Task<(int profilesSeeded, string? error)> SeedAsync(
        string authorizationHeader,
        IReadOnlyList<DemoEmployeeRecord> employees,
        CancellationToken ct)
    {
        var baseUrl = config["DemoSeed:ProfileBaseUrl"] ?? "http://localhost:5197";
        var url = $"{baseUrl.TrimEnd('/')}/profile/admin/demo-seed";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrEmpty(authorizationHeader))
            request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
        request.Content = JsonContent.Create(new { employees });

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return (employees.Count, null);
            var body = await response.Content.ReadAsStringAsync(ct);
            return (0, $"HTTP {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (0, $"Profile service unreachable: {ex.Message}");
        }
    }
}

public sealed class HttpDemoSeedConfigurationClient(HttpClient http, IConfiguration config) : IDemoSeedConfigurationClient
{
    public async Task<(int slotsSeeded, string? error)> SeedAsync(
        string authorizationHeader,
        string locationId,
        IReadOnlyList<DemoSlotRecord> slots,
        DemoPolicyRecord policy,
        CancellationToken ct)
    {
        var baseUrl = config["DemoSeed:ConfigurationBaseUrl"] ?? "http://localhost:5141";
        var url = $"{baseUrl.TrimEnd('/')}/configuration/admin/demo-seed";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrEmpty(authorizationHeader))
            request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
        request.Content = JsonContent.Create(new { locationId, slots, policy });

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return (slots.Count, null);
            var body = await response.Content.ReadAsStringAsync(ct);
            return (0, $"HTTP {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (0, $"Configuration service unreachable: {ex.Message}");
        }
    }
}
