using System.Text;
using Dapr.Client;
using FPS.Notification.Application;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

// AUTH008B (#734) — the transient verification-email transport. The verification link (Secret token
// embedded) must reach the provider send but never a persisted record or a log line.
public sealed class VerificationEmailDeliveryTests
{
    private const string Tenant = "tenant-1";
    private const string Recipient = "jan@greenlogistics.example";
    private const string Link = "https://app.fairspot.net/verify-email?token=abc%26def";

    [Fact]
    public async Task DaprBinding_SendsLinkToProviderBinding_WithSafeMetadata()
    {
        BindingRequest? sent = null;
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.InvokeBindingAsync(It.IsAny<BindingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BindingRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync((BindingRequest r, CancellationToken _) =>
                new BindingResponse(r, ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>()));

        var sut = Delivery(dapr.Object, new DaprSendGridEmailOptions
        {
            BindingName = "notification-email", FromEmail = "noreply@fairspot.net", FromName = "FairSpot"
        });

        var ok = await sut.SendAsync(new VerificationEmailRequest(Tenant, Recipient, Link));

        Assert.True(ok);
        Assert.NotNull(sent);
        Assert.Equal("notification-email", sent!.BindingName);
        Assert.Equal("create", sent.Operation);
        Assert.Equal(Recipient, sent.Metadata["emailTo"]);
        Assert.Equal("Verify your FairSpot email address", sent.Metadata["subject"]);
        Assert.Equal("noreply@fairspot.net", sent.Metadata["emailFrom"]);
        Assert.Equal("FairSpot", sent.Metadata["emailFromName"]);
        var body = Encoding.UTF8.GetString(sent.Data.ToArray());
        Assert.Contains("https://app.fairspot.net/verify-email?token=abc%26def", body); // link present for the send
    }

    [Fact]
    public async Task DaprBinding_HtmlEncodesTheLink_InTheBody()
    {
        BindingRequest? sent = null;
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.InvokeBindingAsync(It.IsAny<BindingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BindingRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync((BindingRequest r, CancellationToken _) =>
                new BindingResponse(r, ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>()));

        // A raw ampersand in the link must be HTML-encoded inside the anchor href to keep the markup valid.
        await Delivery(dapr.Object).SendAsync(
            new VerificationEmailRequest(Tenant, Recipient, "https://app.fairspot.net/v?token=a&x=1"));

        var body = Encoding.UTF8.GetString(sent!.Data.ToArray());
        Assert.Contains("token=a&amp;x=1", body);
    }

    [Fact]
    public async Task DaprBinding_RejectsInvalidRecipient_WithoutInvokingBinding()
    {
        var dapr = new Mock<DaprClient>();

        var ok = await Delivery(dapr.Object).SendAsync(new VerificationEmailRequest(Tenant, "not-an-email", Link));

        Assert.False(ok);
        dapr.Verify(d => d.InvokeBindingAsync(It.IsAny<BindingRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DaprBinding_ProviderException_ReturnsFalse_WithoutLeakingLink()
    {
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.InvokeBindingAsync(It.IsAny<BindingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider-detail"));

        var ok = await Delivery(dapr.Object).SendAsync(new VerificationEmailRequest(Tenant, Recipient, Link));

        Assert.False(ok); // failure is swallowed to a bool; no link/token surfaces to the caller
    }

    [Fact]
    public async Task LogSafe_ReturnsTrue_WithoutAnyProviderCall()
    {
        var ok = await new LogSafeVerificationEmailDelivery(
            NullLogger<LogSafeVerificationEmailDelivery>.Instance)
            .SendAsync(new VerificationEmailRequest(Tenant, Recipient, Link));

        Assert.True(ok);
    }

    private static DaprBindingVerificationEmailDelivery Delivery(DaprClient dapr, DaprSendGridEmailOptions? options = null) =>
        new(dapr, Options.Create(options ?? new DaprSendGridEmailOptions()),
            NullLogger<DaprBindingVerificationEmailDelivery>.Instance);
}
