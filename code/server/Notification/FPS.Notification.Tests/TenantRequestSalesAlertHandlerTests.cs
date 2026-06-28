using FPS.Notification.Application;
using FPS.Notification.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Notification.Tests;

public sealed class TenantRequestSalesAlertHandlerTests
{
    private static TenantRequestEvent Event() => new("req-1", "Acme", "acme.com", DateTimeOffset.UtcNow);

    private static IConfiguration Config(string? salesEmail = null)
    {
        var dict = new Dictionary<string, string?>();
        if (salesEmail is not null) dict["Onboarding:SalesEmail"] = salesEmail;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static (TenantRequestSalesAlertHandler handler, Mock<IEmailNotificationSender> sender, Func<NotificationRecord?> sent) Build(
        IConfiguration config, EmailSendResult? result = null)
    {
        NotificationRecord? captured = null;
        var sender = new Mock<IEmailNotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRecord, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(result ?? EmailSendResult.Ok());
        var handler = new TenantRequestSalesAlertHandler(sender.Object, config, NullLogger<TenantRequestSalesAlertHandler>.Instance);
        return (handler, sender, () => captured);
    }

    [Fact]
    public async Task Handle_EmailsConfiguredSalesAddress_WithAlertOnly()
    {
        var (handler, _, sent) = Build(Config("ops@fairspot.net"));

        await handler.HandleAsync(Event(), CancellationToken.None);

        var record = sent();
        Assert.NotNull(record);
        Assert.Equal("ops@fairspot.net", record!.RecipientId);
        Assert.Equal(NotificationChannel.Email, record.Channel);
        Assert.Contains("Acme", record.MessageText);
        Assert.Contains("req-1", record.MessageText);
    }

    [Fact]
    public async Task Handle_DefaultsToSalesFairspot_WhenUnconfigured()
    {
        var (handler, _, sent) = Build(Config());

        await handler.HandleAsync(Event(), CancellationToken.None);

        Assert.Equal("sales@fairspot.net", sent()!.RecipientId);
    }

    [Fact]
    public async Task Handle_DeliveryThrows_IsSwallowed()
    {
        var sender = new Mock<IEmailNotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));
        var handler = new TenantRequestSalesAlertHandler(sender.Object, Config(), NullLogger<TenantRequestSalesAlertHandler>.Instance);

        // A delivery failure must not propagate (the request is already recorded + queued).
        await handler.HandleAsync(Event(), CancellationToken.None);
    }
}
