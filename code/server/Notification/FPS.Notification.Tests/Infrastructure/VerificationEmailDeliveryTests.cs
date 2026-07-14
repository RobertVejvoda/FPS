using FPS.Notification.Application;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

// AUTH008B (#734) + NOTIF (#731) — the transient verification-email transport. The verification link (Secret
// token embedded) must reach the provider send but never a persisted record or a log line, and the email now
// goes out multipart (HTML + plain text).
public sealed class VerificationEmailDeliveryTests
{
    private const string Tenant = "tenant-1";
    private const string Recipient = "jan@greenlogistics.example";
    private const string Link = "https://app.fairspot.net/verify-email?token=abc%26def";

    [Fact]
    public async Task DaprBinding_SendsBothHtmlAndTextParts_WithSubjectAndRecipient()
    {
        var transport = new Mock<ISendGridEmailTransport>();
        SendGridEmailMessage? sent = null;
        transport.Setup(t => t.SendAsync(It.IsAny<SendGridEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridEmailMessage, CancellationToken>((m, _) => sent = m)
            .ReturnsAsync(true);

        var ok = await Delivery(transport.Object).SendAsync(new VerificationEmailRequest(Tenant, Recipient, Link));

        Assert.True(ok);
        Assert.NotNull(sent);
        Assert.Equal(Recipient, sent!.ToEmail);
        Assert.Equal("Verify your FairSpot email address", sent.Subject);
        Assert.NotNull(sent.TextBody);                                   // plain-text part present (multipart)
        Assert.Contains(Link, sent.HtmlBody);                            // link present in HTML
        Assert.Contains(Link, sent.TextBody!);                           // link present in plain text
    }

    [Fact]
    public async Task DaprBinding_HtmlEncodesTheLink_ButPlainTextKeepsItRaw()
    {
        var transport = new Mock<ISendGridEmailTransport>();
        SendGridEmailMessage? sent = null;
        transport.Setup(t => t.SendAsync(It.IsAny<SendGridEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridEmailMessage, CancellationToken>((m, _) => sent = m)
            .ReturnsAsync(true);

        await Delivery(transport.Object).SendAsync(
            new VerificationEmailRequest(Tenant, Recipient, "https://app.fairspot.net/v?token=a&x=1"));

        Assert.Contains("token=a&amp;x=1", sent!.HtmlBody);              // ampersand encoded in the HTML anchor
        Assert.Contains("token=a&x=1", sent.TextBody!);                 // raw in the plain-text part
    }

    [Fact]
    public async Task DaprBinding_RejectsInvalidRecipient_WithoutCallingTransport()
    {
        var transport = new Mock<ISendGridEmailTransport>();

        var ok = await Delivery(transport.Object).SendAsync(new VerificationEmailRequest(Tenant, "not-an-email", Link));

        Assert.False(ok);
        transport.Verify(t => t.SendAsync(It.IsAny<SendGridEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DaprBinding_TransportFailure_ReturnsFalse()
    {
        var transport = new Mock<ISendGridEmailTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<SendGridEmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ok = await Delivery(transport.Object).SendAsync(new VerificationEmailRequest(Tenant, Recipient, Link));

        Assert.False(ok);
    }

    [Fact]
    public async Task LogSafe_ReturnsTrue_WithoutAnyProviderCall()
    {
        var ok = await new LogSafeVerificationEmailDelivery(
            NullLogger<LogSafeVerificationEmailDelivery>.Instance)
            .SendAsync(new VerificationEmailRequest(Tenant, Recipient, Link));

        Assert.True(ok);
    }

    // ── Locale plumbing (LOC001 #744) ─────────────────────────────────────────

    [Fact]
    public async Task DaprBinding_CzechLocale_SubjectIsLocalized()
    {
        var transport = new Mock<ISendGridEmailTransport>();
        SendGridEmailMessage? sent = null;
        transport.Setup(t => t.SendAsync(It.IsAny<SendGridEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridEmailMessage, CancellationToken>((m, _) => sent = m)
            .ReturnsAsync(true);

        await Delivery(transport.Object, "cs-CZ").SendAsync(new VerificationEmailRequest(Tenant, Recipient, Link));

        Assert.NotNull(sent);
        Assert.Equal("Ověřte svou e-mailovou adresu FairSpot", sent!.Subject);
        // TextBody is not HTML-entity-encoded, so it is the reliable place to assert raw Czech
        // diacritics; HtmlEncode legitimately numeric-escapes non-ASCII characters in HtmlBody.
        Assert.Contains("Potvrďte svou e-mailovou adresu", sent.TextBody);
    }

    [Fact]
    public async Task DaprBinding_UnknownLocale_FallsBackToEnglishSubject()
    {
        var transport = new Mock<ISendGridEmailTransport>();
        SendGridEmailMessage? sent = null;
        transport.Setup(t => t.SendAsync(It.IsAny<SendGridEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridEmailMessage, CancellationToken>((m, _) => sent = m)
            .ReturnsAsync(true);

        await Delivery(transport.Object, "de-DE").SendAsync(new VerificationEmailRequest(Tenant, Recipient, Link));

        Assert.NotNull(sent);
        Assert.Equal("Verify your FairSpot email address", sent!.Subject);
    }

    private static DaprBindingVerificationEmailDelivery Delivery(ISendGridEmailTransport transport) =>
        new(transport, NullLogger<DaprBindingVerificationEmailDelivery>.Instance);

    private static DaprBindingVerificationEmailDelivery Delivery(ISendGridEmailTransport transport, string locale) =>
        new(transport, NullLogger<DaprBindingVerificationEmailDelivery>.Instance,
            Options.Create(new NotificationLocaleOptions { DefaultLocale = locale }));
}
