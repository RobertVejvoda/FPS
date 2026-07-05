using System.Net;
using System.Text.Json;
using Dapr.Client;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

// NOTIF #731 — the real SendGrid v3 HTTP transport. These tests prove the actual outbound request carries
// BOTH a text/plain and a text/html content part (multipart/alternative), authenticates with the key read
// from the Dapr secret store, and maps provider outcomes without leaking secrets. No live send.
public sealed class SendGridHttpEmailTransportTests
{
    private const string ApiKey = "SG.unit-test-key";

    [Fact]
    public async Task SendAsync_PostsMultipartRequest_WithBothContentParts_AndBearerAuth()
    {
        var handler = new CapturingHandler(HttpStatusCode.Accepted);
        var transport = Transport(handler);

        var ok = await transport.SendAsync(new SendGridEmailMessage(
            "jan@greenlogistics.example", "Jan", "Your parking spot is confirmed",
            "<p>HTML body</p>", "Plain body"));

        Assert.True(ok);
        Assert.Equal("https://api.sendgrid.com/v3/mail/send", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal(ApiKey, handler.Request.Headers.Authorization.Parameter);

        // Parse the wire body (JSON escapes < / > by design; the provider decodes them back).
        var parts = ContentParts(handler.Body!);
        Assert.Equal(2, parts.Count);
        Assert.Equal("text/plain", parts[0].Type);              // plain text first (ascending preference)
        Assert.Equal("Plain body", parts[0].Value);
        Assert.Equal("text/html", parts[1].Type);               // HTML last (preferred)
        Assert.Equal("<p>HTML body</p>", parts[1].Value);

        using var doc = JsonDocument.Parse(handler.Body!);
        Assert.Equal("noreply@fairspot.net", doc.RootElement.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal("jan@greenlogistics.example",
            doc.RootElement.GetProperty("personalizations")[0].GetProperty("to")[0].GetProperty("email").GetString());
    }

    [Fact]
    public async Task SendAsync_WithoutTextBody_SendsHtmlPartOnly()
    {
        var handler = new CapturingHandler(HttpStatusCode.Accepted);
        var transport = Transport(handler);

        var ok = await transport.SendAsync(new SendGridEmailMessage(
            "jan@greenlogistics.example", null, "Subject", "<p>HTML only</p>", null));

        Assert.True(ok);
        var parts = ContentParts(handler.Body!);
        Assert.Single(parts);
        Assert.Equal("text/html", parts[0].Type);
    }

    [Fact]
    public async Task SendAsync_ProviderRejection_ReturnsFalse()
    {
        var transport = Transport(new CapturingHandler(HttpStatusCode.Unauthorized));

        var ok = await transport.SendAsync(new SendGridEmailMessage(
            "jan@greenlogistics.example", null, "Subject", "<p>x</p>", "x"));

        Assert.False(ok);
    }

    [Fact]
    public async Task SendAsync_ProviderException_ReturnsFalse_WithoutThrowing()
    {
        var transport = Transport(new ThrowingHandler());

        var ok = await transport.SendAsync(new SendGridEmailMessage(
            "jan@greenlogistics.example", null, "Subject", "<p>x</p>", "x"));

        Assert.False(ok);
    }

    [Fact]
    public async Task SendAsync_MissingApiKeyInSecret_ReturnsFalse_WithoutCallingProvider()
    {
        var handler = new CapturingHandler(HttpStatusCode.Accepted);
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.GetSecretAsync("secretstore", "sendgrid-credentials",
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>()); // no apiKey key
        var transport = new SendGridHttpEmailTransport(
            FactoryFor(handler), dapr.Object, Options.Create(SendGridOptions()),
            NullLogger<SendGridHttpEmailTransport>.Instance);

        var ok = await transport.SendAsync(new SendGridEmailMessage(
            "jan@greenlogistics.example", null, "Subject", "<p>x</p>", "x"));

        Assert.False(ok);
        Assert.Null(handler.Request); // never reached the provider
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static SendGridHttpEmailTransport Transport(HttpMessageHandler handler)
    {
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.GetSecretAsync("secretstore", "sendgrid-credentials",
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["apiKey"] = ApiKey });
        return new SendGridHttpEmailTransport(
            FactoryFor(handler), dapr.Object, Options.Create(SendGridOptions()),
            NullLogger<SendGridHttpEmailTransport>.Instance);
    }

    private static DaprSendGridEmailOptions SendGridOptions() =>
        new() { Provider = "SendGrid", FromEmail = "noreply@fairspot.net", FromName = "FairSpot" };

    private static List<(string Type, string Value)> ContentParts(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("content").EnumerateArray()
            .Select(e => (e.GetProperty("type").GetString()!, e.GetProperty("value").GetString()!))
            .ToList();
    }

    private static IHttpClientFactory FactoryFor(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(SendGridHttpEmailTransport.HttpClientName))
            .Returns(() => new HttpClient(handler));
        return factory.Object;
    }

    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(status);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("provider unreachable");
    }
}
