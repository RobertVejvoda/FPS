using FPS.Notification.Application;
using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

public sealed class DaprSendGridEmailNotificationSenderTests
{
    [Fact]
    public async Task SendAsync_ForwardsBothHtmlAndTextParts_ToTransport()
    {
        var transport = new Mock<ISendGridEmailTransport>();
        SendGridEmailMessage? sent = null;
        transport.Setup(t => t.SendAsync(It.IsAny<SendGridEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridEmailMessage, CancellationToken>((m, _) => sent = m)
            .ReturnsAsync(true);
        var sender = new DaprSendGridEmailNotificationSender(transport.Object, NullLogger<DaprSendGridEmailNotificationSender>.Instance);

        // NOTIF #731 — both the HTML and plain-text bodies must reach the transport (multipart delivery).
        var composed = new ComposedEmail(
            "Your parking spot is confirmed",
            "<p>Hello &lt;ops&gt;<br>Review request.</p>",
            "Hello <ops>\nReview request.");
        var result = await sender.SendAsync(Record("ops@fairspot.net"), "ops@fairspot.net", composed);

        Assert.True(result.Success);
        Assert.NotNull(sent);
        Assert.Equal("ops@fairspot.net", sent!.ToEmail);
        Assert.Equal("Your parking spot is confirmed", sent.Subject);
        Assert.Equal("<p>Hello &lt;ops&gt;<br>Review request.</p>", sent.HtmlBody);
        Assert.Equal("Hello <ops>\nReview request.", sent.TextBody);
    }

    [Fact]
    public async Task SendAsync_RejectsNonEmailRecipientId_WithoutCallingTransport()
    {
        var transport = new Mock<ISendGridEmailTransport>();
        var sender = new DaprSendGridEmailNotificationSender(transport.Object, NullLogger<DaprSendGridEmailNotificationSender>.Instance);

        var result = await sender.SendAsync(Record("user-1"), "user-1", Email());

        Assert.False(result.Success);
        Assert.Equal(EmailFailureCategory.DeliveryRejected, result.FailureCategory);
        transport.Verify(t => t.SendAsync(It.IsAny<SendGridEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_TransportFailure_ReturnsProviderUnavailable()
    {
        var transport = new Mock<ISendGridEmailTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<SendGridEmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sender = new DaprSendGridEmailNotificationSender(transport.Object, NullLogger<DaprSendGridEmailNotificationSender>.Instance);

        var result = await sender.SendAsync(Record("ops@fairspot.net"), "ops@fairspot.net", Email());

        Assert.False(result.Success);
        Assert.Equal("Email delivery unavailable", result.FailureReason);
        Assert.Equal(EmailFailureCategory.ProviderUnavailable, result.FailureCategory);
    }

    [Theory]
    [InlineData("SendGrid")]
    [InlineData("DaprSendGrid")]
    [InlineData("DaprBinding")]
    public void IsConfiguredProvider_AcceptsSendGridBindingAliases(string provider)
    {
        Assert.True(DaprSendGridEmailNotificationSender.IsConfiguredProvider(provider));
    }

    private static ComposedEmail Email(
        string subject = "Parking spot allocated",
        string html = "<p>Body</p>",
        string text = "Body") => new(subject, html, text);

    private static NotificationRecord Record(
        string recipientId,
        string message = "A FairSpot notification.") => new()
    {
        Id = Guid.NewGuid(),
        DeduplicationKey = "event-1:recipient:email",
        TenantId = "tenant-1",
        RecipientId = recipientId,
        NotificationType = "booking.slotAllocated",
        Channel = NotificationChannel.Email,
        MessageText = message,
        SourceEventId = "event-1",
        CreatedAt = DateTime.UtcNow
    };
}
