using FPS.Notification.Application;
using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Notification.Tests;

public sealed class TenantRequestSalesAlertHandlerTests
{
    private static TenantRequestEvent Event(string id = "req-1") => new(id, "Acme", "acme.com", DateTimeOffset.UtcNow);

    private static IConfiguration Config(string? salesEmail = null)
    {
        var dict = new Dictionary<string, string?>();
        if (salesEmail is not null) dict["Onboarding:SalesEmail"] = salesEmail;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static Mock<IEmailNotificationSender> OkSender(Action<NotificationRecord>? capture = null)
    {
        var sender = new Mock<IEmailNotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRecord, CancellationToken>((r, _) => capture?.Invoke(r))
            .ReturnsAsync(EmailSendResult.Ok());
        return sender;
    }

    private static TenantRequestSalesAlertHandler Handler(
        INotificationRepository repo, IEmailNotificationSender sender, IConfiguration config) =>
        new(repo, sender, config, NullLogger<TenantRequestSalesAlertHandler>.Instance);

    [Fact]
    public async Task Handle_EmailsConfiguredSalesAddress_WithAlertOnly()
    {
        NotificationRecord? sent = null;
        var handler = Handler(new InMemoryNotificationRepository(), OkSender(r => sent = r).Object, Config("ops@fairspot.net"));

        await handler.HandleAsync(Event(), CancellationToken.None);

        Assert.NotNull(sent);
        Assert.Equal("ops@fairspot.net", sent!.RecipientId);
        Assert.Equal(NotificationChannel.Email, sent.Channel);
        Assert.Contains("Acme", sent.MessageText);
        Assert.Contains("req-1", sent.MessageText);
    }

    [Fact]
    public async Task Handle_DefaultsToSalesFairspot_WhenUnconfigured()
    {
        NotificationRecord? sent = null;
        var handler = Handler(new InMemoryNotificationRepository(), OkSender(r => sent = r).Object, Config());

        await handler.HandleAsync(Event(), CancellationToken.None);

        Assert.Equal("sales@fairspot.net", sent!.RecipientId);
    }

    [Fact]
    public async Task Handle_ReplayedEvent_SendsEmailOnce()
    {
        var sender = OkSender();
        var handler = Handler(new InMemoryNotificationRepository(), sender.Object, Config());

        await handler.HandleAsync(Event("dup-1"), CancellationToken.None);
        await handler.HandleAsync(Event("dup-1"), CancellationToken.None); // at-least-once replay of the same event

        sender.Verify(s => s.SendAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DeliveryFails_PersistsFailedRecord_WithoutThrowing()
    {
        var repo = new InMemoryNotificationRepository();
        var sender = new Mock<IEmailNotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));
        var handler = Handler(repo, sender.Object, Config("ops@fairspot.net"));

        await handler.HandleAsync(Event("fail-1"), CancellationToken.None); // must not throw

        var saved = await repo.GetByRecipientAsync("platform", "ops@fairspot.net", cancellationToken: CancellationToken.None);
        var record = Assert.Single(saved);
        Assert.Equal(NotificationDeliveryStatus.Failed, record.DeliveryStatus);
    }
}
