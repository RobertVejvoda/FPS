using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FPS.Customer.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// Verifies a Turnstile token against Cloudflare's siteverify endpoint. Verification is skipped
/// only in <b>Development</b> when no secret is configured (so local/eval runs without the widget);
/// in any other profile a missing secret is a misconfiguration that <b>fails closed</b> rather than
/// exposing the public unauthenticated endpoint. Every other path also fails closed: a missing
/// token, a non-2xx response, or an unreachable Cloudflare all return false.
/// </summary>
public sealed class HttpTurnstileVerifier(
    HttpClient http, IConfiguration configuration, IHostEnvironment environment,
    ILogger<HttpTurnstileVerifier> logger) : ITurnstileVerifier
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct)
    {
        var secret = configuration["Turnstile:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning("Turnstile secret not configured; skipping verification (Development only).");
                return true;
            }

            logger.LogError(
                "Turnstile secret missing in '{Environment}' — failing closed on the public intake.",
                environment.EnvironmentName);
            return false;
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
