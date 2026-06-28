using Dapr.Client;
using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Customer.Tests;

public sealed class DaprTenantRequestNotifierTests
{
    private static TenantRequest Request() => new()
    {
        RequestId = "req-1",
        Company = "Acme",
        PrimaryDomain = "acme.com",
        ContactEmail = "jo@acme.com",
        Message = "please call me back",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task NotifySales_PublishesAlert_WithoutContactPii()
    {
        var dapr = new Mock<DaprClient>();
        TenantRequestEvent? published = null;
        dapr.Setup(d => d.PublishEventAsync(
                "fps-pubsub", "tenant-request-received", It.IsAny<TenantRequestEvent>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, TenantRequestEvent, CancellationToken>((_, _, e, _) => published = e)
            .Returns(Task.CompletedTask);

        var notifier = new DaprTenantRequestNotifier(dapr.Object, NullLogger<DaprTenantRequestNotifier>.Instance);
        await notifier.NotifySalesAsync(Request(), CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal("req-1", published!.RequestId);
        Assert.Equal("Acme", published.Company);

        // The prospect's contact email and message never travel on the event.
        var json = System.Text.Json.JsonSerializer.Serialize(published);
        Assert.DoesNotContain("jo@acme.com", json);
        Assert.DoesNotContain("please call me back", json);
    }

    [Fact]
    public async Task NotifySales_PublishFails_DoesNotThrow()
    {
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.PublishEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TenantRequestEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sidecar down"));

        var notifier = new DaprTenantRequestNotifier(dapr.Object, NullLogger<DaprTenantRequestNotifier>.Instance);

        // Intake must stay successful even when the alert can't be published.
        await notifier.NotifySalesAsync(Request(), CancellationToken.None);
    }
}
