using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FPS.Notification.Infrastructure;

/// <summary>
/// NOTIF #731 — real SendGrid transport that sends a genuine multipart/alternative email carrying BOTH the
/// plain-text and HTML parts. The Dapr `bindings.twilio.sendgrid` binding can only send a single `text/html`
/// content part (verified against the binding spec), so — per the Dapr-first fallback rule — this is a thin
/// direct call to the SendGrid v3 Mail Send API. The API key is read from the Dapr secret store (the same
/// `sendgrid-credentials` secret the binding used); no key is held in configuration or Git.
/// </summary>
public interface ISendGridEmailTransport
{
    Task<bool> SendAsync(SendGridEmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>A composed email ready to send. TextBody is optional; when present it is sent as the plain-text alternative.</summary>
public sealed record SendGridEmailMessage(string ToEmail, string? ToName, string Subject, string HtmlBody, string? TextBody);

public sealed class SendGridHttpEmailTransport(
    IHttpClientFactory httpClientFactory,
    DaprClient daprClient,
    IOptions<DaprSendGridEmailOptions> options,
    ILogger<SendGridHttpEmailTransport> logger) : ISendGridEmailTransport
{
    public const string HttpClientName = "sendgrid";
    private const string SendEndpoint = "https://api.sendgrid.com/v3/mail/send";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SemaphoreSlim apiKeyGate = new(1, 1);
    private string? cachedApiKey;

    public async Task<bool> SendAsync(SendGridEmailMessage message, CancellationToken cancellationToken = default)
    {
        string apiKey;
        try
        {
            apiKey = await GetApiKeyAsync(cancellationToken);
        }
        catch (Exception)
        {
            // No secret material in the log.
            logger.LogWarning("Email not sent: SendGrid API key unavailable from the secret store.");
            return false;
        }

        var configured = options.Value;

        // SendGrid orders content parts by ascending preference — plain text first, HTML last (preferred).
        var content = new List<SendGridContent>(2);
        if (!string.IsNullOrEmpty(message.TextBody))
        {
            content.Add(new SendGridContent("text/plain", message.TextBody));
        }
        content.Add(new SendGridContent("text/html", message.HtmlBody));

        var payload = new SendGridPayload(
            [new SendGridPersonalization([new SendGridAddress(message.ToEmail, NullIfBlank(message.ToName))])],
            new SendGridAddress(configured.FromEmail?.Trim() ?? string.Empty, NullIfBlank(configured.FromName)),
            message.Subject,
            content);

        using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // Status only — never the provider body (it can echo request content).
            logger.LogWarning("SendGrid rejected the email. Status={Status}", (int)response.StatusCode);
            return false;
        }
        catch (Exception)
        {
            logger.LogWarning("SendGrid email send failed (provider unavailable).");
            return false;
        }
    }

    private async Task<string> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        if (cachedApiKey is not null)
        {
            return cachedApiKey;
        }

        await apiKeyGate.WaitAsync(cancellationToken);
        try
        {
            if (cachedApiKey is not null)
            {
                return cachedApiKey;
            }

            var configured = options.Value;
            var secret = await daprClient.GetSecretAsync(
                configured.SecretStoreName, configured.ApiKeySecretName, cancellationToken: cancellationToken);
            if (!secret.TryGetValue(configured.ApiKeySecretKey, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("SendGrid API key not found in the configured secret.");
            }

            cachedApiKey = apiKey;
            return apiKey;
        }
        finally
        {
            apiKeyGate.Release();
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // SendGrid v3 Mail Send request shape (only the fields we use).
    private sealed record SendGridPayload(
        [property: JsonPropertyName("personalizations")] IReadOnlyList<SendGridPersonalization> Personalizations,
        [property: JsonPropertyName("from")] SendGridAddress From,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("content")] IReadOnlyList<SendGridContent> Content);

    private sealed record SendGridPersonalization(
        [property: JsonPropertyName("to")] IReadOnlyList<SendGridAddress> To);

    private sealed record SendGridAddress(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record SendGridContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] string Value);
}
