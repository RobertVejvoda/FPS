using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FPS.Customer.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// Verifies a Turnstile token against Cloudflare's siteverify endpoint. When no secret is
/// configured (local/eval profiles), verification is skipped — the widget is not enforced — but
/// every other path fails closed: a missing token, a non-2xx response, or an unreachable
/// Cloudflare all return false.
/// </summary>
public sealed class HttpTurnstileVerifier(
    HttpClient http, IConfiguration configuration, ILogger<HttpTurnstileVerifier> logger) : ITurnstileVerifier
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct)
    {
        var secret = configuration["Turnstile:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogInformation("Turnstile secret not configured; skipping verification (non-production path).");
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var form = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token };
            if (!string.IsNullOrWhiteSpace(remoteIp)) form["remoteip"] = remoteIp;

            using var response = await http.PostAsync(VerifyUrl, new FormUrlEncodedContent(form), ct);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<TurnstileResult>(ct);
            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Turnstile verification could not reach Cloudflare; failing closed.");
            return false;
        }
    }

    private sealed record TurnstileResult([property: JsonPropertyName("success")] bool Success);
}
