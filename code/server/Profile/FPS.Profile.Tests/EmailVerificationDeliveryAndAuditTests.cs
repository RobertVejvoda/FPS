using System.Security.Cryptography;
using System.Text;
using Dapr.Client;
using FPS.Profile.Application;
using FPS.Profile.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FPS.Profile.Tests;

// AUTH008B (#734) — Profile-side delivery handoff and durable audit. Proves the verification link carries
// the token to Notification (transient, not persisted here), and that audit evidence is pseudonymised and
// free of token/email.
public sealed class EmailVerificationDeliveryAndAuditTests
{
    private const string Tenant = "tenant-1";
    private const string User = "user-1";
    private const string Email = "jan@greenlogistics.example";
    private const string Token = "one-time-token-abc";

    // ── Delivery handoff ──────────────────────────────────────────────────────

    [Fact]
    public async Task Sender_BuildsLinkWithToken_AndHandsToNotification()
    {
        var client = new Mock<INotificationVerificationClient>();
        string? deliveredLink = null;
        client.Setup(c => c.DeliverAsync(Tenant, Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, link, _) => deliveredLink = link)
            .Returns(Task.CompletedTask);
        var sender = new DaprNotificationEmailVerificationSender(
            client.Object, Options.Create(new EmailVerificationOptions { VerificationBaseUrl = "https://app.fairspot.net/verify-email" }),
            NullLogger<DaprNotificationEmailVerificationSender>.Instance);

        await sender.SendAsync(Tenant, User, Email, Token);

        Assert.NotNull(deliveredLink);
        Assert.StartsWith("https://app.fairspot.net/verify-email?token=", deliveredLink);
        Assert.Contains(Uri.EscapeDataString(Token), deliveredLink!);
    }

    [Fact]
    public void BuildLink_AppendsTokenAsQuery_HandlingExistingQuery()
    {
        Assert.Equal("https://x/v?token=t", DaprNotificationEmailVerificationSender.BuildLink("https://x/v", "t"));
        Assert.Equal("https://x/v?a=1&token=t", DaprNotificationEmailVerificationSender.BuildLink("https://x/v?a=1", "t"));
    }

    [Fact]
    public async Task Sender_SwallowsClientFailure_DoesNotThrow()
    {
        var client = new Mock<INotificationVerificationClient>();
        client.Setup(c => c.DeliverAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notification down"));
        var sender = new DaprNotificationEmailVerificationSender(
            client.Object, Options.Create(new EmailVerificationOptions()), NullLogger<DaprNotificationEmailVerificationSender>.Instance);

        var ex = await Record.ExceptionAsync(() => sender.SendAsync(Tenant, User, Email, Token));

        Assert.Null(ex);
    }

    // ── Audit evidence ────────────────────────────────────────────────────────

    [Fact]
    public async Task Audit_Failed_PublishesHashedActor_NoRawUserId_NoToken_NoEmail()
    {
        var dapr = new Mock<DaprClient>();
        SecurityAuditEvent? published = null;
        dapr.Setup(d => d.PublishEventAsync("fairspot-pubsub", "security-events", It.IsAny<SecurityAuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, SecurityAuditEvent, CancellationToken>((_, _, e, _) => published = e)
            .Returns(Task.CompletedTask);
        var audit = new DaprEmailVerificationAudit(dapr.Object);

        await audit.FailedAsync(Tenant, User, "invalid_token");

        Assert.NotNull(published);
        Assert.Equal("email-verification", published!.Category);
        Assert.Equal("failed", published.Outcome);
        Assert.Equal("invalid_token", published.Reason);
        Assert.Equal(ExpectedHash(User), published.ActorHash);
        Assert.NotEqual(User, published.ActorHash);           // never the raw user id
        var json = System.Text.Json.JsonSerializer.Serialize(published);
        Assert.DoesNotContain(User, json);                    // no raw user id anywhere
        Assert.DoesNotContain(Email, json);                   // no email
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("succeeded")]
    [InlineData("expired")]
    public async Task Audit_Outcomes_PublishToSecurityEventsTopic(string outcome)
    {
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.PublishEventAsync("fairspot-pubsub", "security-events", It.IsAny<SecurityAuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var audit = new DaprEmailVerificationAudit(dapr.Object);

        Task call = outcome switch
        {
            "requested" => audit.RequestedAsync(Tenant, User),
            "succeeded" => audit.SucceededAsync(Tenant, User),
            _ => audit.ExpiredAsync(Tenant, User),
        };
        await call;

        dapr.Verify(d => d.PublishEventAsync("fairspot-pubsub", "security-events",
            It.Is<SecurityAuditEvent>(e => e.Outcome == outcome && e.ActorHash == ExpectedHash(User)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static string ExpectedHash(string userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();
}
