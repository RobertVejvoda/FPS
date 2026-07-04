using System.Text;
using Dapr.Client;
using FPS.Notification.Application;
using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

public sealed class DaprSendGridEmailNotificationSenderTests
{
    [Fact]
    public async Task SendAsync_InvokesNotificationEmailBinding_WithSafeMetadata()
    {
        BindingRequest? sentRequest = null;
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.InvokeBindingAsync(
                It.IsAny<BindingRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<BindingRequest, CancellationToken>((request, _) => sentRequest = request)
            .ReturnsAsync((BindingRequest request, CancellationToken _) =>
                new BindingResponse(request, ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>()));

        var sender = Sender(dapr.Object, new DaprSendGridEmailOptions
        {
            BindingName = "notification-email",
            SubjectPrefix = "FairSpot",
            FromEmail = "noreply@fairspot.net",
            FromName = "FairSpot"
        });

        // NOTIF #727 — the sender forwards already-composed subject/body verbatim; it does not build them.
        var composed = new ComposedEmail(
            "Your parking spot is confirmed",
            "<p>Hello &lt;ops&gt;<br>Review request.</p>",
            "Hello <ops>\nReview request.");
        var result = await sender.SendAsync(Record("ops@fairspot.net"), "ops@fairspot.net", composed);

        Assert.True(result.Success);
        Assert.NotNull(sentRequest);
        Assert.Equal("notification-email", sentRequest!.BindingName);
        Assert.Equal("create", sentRequest.Operation);
        Assert.Equal("ops@fairspot.net", sentRequest.Metadata["emailTo"]);
        Assert.Equal("Your parking spot is confirmed", sentRequest.Metadata["subject"]);
        Assert.Equal("noreply@fairspot.net", sentRequest.Metadata["emailFrom"]);
        Assert.Equal("FairSpot", sentRequest.Metadata["emailFromName"]);
        var body = Encoding.UTF8.GetString(sentRequest.Data.ToArray());
        Assert.Contains("Hello &lt;ops&gt;<br>Review request.", body);
    }

    [Fact]
    public async Task SendAsync_RejectsNonEmailRecipientId_WithoutInvokingBinding()
    {
        var dapr = new Mock<DaprClient>();
        var sender = Sender(dapr.Object);

        var result = await sender.SendAsync(Record("user-1"), "user-1", Email());

        Assert.False(result.Success);
        Assert.Equal(EmailFailureCategory.DeliveryRejected, result.FailureCategory);
        dapr.Verify(d => d.InvokeBindingAsync(
                It.IsAny<BindingRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_ProviderException_ReturnsProviderUnavailable_WithoutProviderDetails()
    {
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.InvokeBindingAsync(
                It.IsAny<BindingRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider-secret-detail"));
        var sender = Sender(dapr.Object);

        var result = await sender.SendAsync(Record("ops@fairspot.net"), "ops@fairspot.net", Email());

        Assert.False(result.Success);
        Assert.Equal("Email delivery unavailable", result.FailureReason);
        Assert.Equal(EmailFailureCategory.ProviderUnavailable, result.FailureCategory);
        Assert.DoesNotContain("provider-secret-detail", result.FailureReason);
    }

    [Theory]
    [InlineData("SendGrid")]
    [InlineData("DaprSendGrid")]
    [InlineData("DaprBinding")]
    public void IsConfiguredProvider_AcceptsSendGridBindingAliases(string provider)
    {
        Assert.True(DaprSendGridEmailNotificationSender.IsConfiguredProvider(provider));
    }

    private static DaprSendGridEmailNotificationSender Sender(
        DaprClient dapr,
        DaprSendGridEmailOptions? options = null) =>
        new(
            dapr,
            Options.Create(options ?? new DaprSendGridEmailOptions()),
            NullLogger<DaprSendGridEmailNotificationSender>.Instance);

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
