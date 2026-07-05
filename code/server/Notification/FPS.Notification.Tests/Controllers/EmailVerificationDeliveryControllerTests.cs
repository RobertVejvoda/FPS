using FPS.Notification.Application;
using FPS.Notification.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Notification.Tests.Controllers;

// AUTH008B (#734) — the internal endpoint Profile invokes. It hands the request to the delivery seam and
// returns only an outcome; the response body never echoes the link/token.
public sealed class EmailVerificationDeliveryControllerTests
{
    private static readonly VerificationEmailDeliveryRequest Valid =
        new("tenant-1", "jan@greenlogistics.example", "https://app.fairspot.net/verify-email?token=abc");

    [Fact]
    public async Task Send_DelegatesToDelivery_AndReturnsOkOnSuccess()
    {
        var delivery = new Mock<IVerificationEmailDelivery>();
        VerificationEmailRequest? seen = null;
        delivery.Setup(d => d.SendAsync(It.IsAny<VerificationEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<VerificationEmailRequest, CancellationToken>((r, _) => seen = r)
            .ReturnsAsync(true);

        var result = await new EmailVerificationDeliveryController(delivery.Object).Send(Valid, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<VerificationEmailDeliveryResult>(ok.Value).Sent);
        Assert.Equal("tenant-1", seen!.TenantId);
        Assert.Equal(Valid.VerificationLink, seen.VerificationLink);
    }

    [Fact]
    public async Task Send_ReturnsBadGateway_WhenDeliveryFails()
    {
        var delivery = new Mock<IVerificationEmailDelivery>();
        delivery.Setup(d => d.SendAsync(It.IsAny<VerificationEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await new EmailVerificationDeliveryController(delivery.Object).Send(Valid, default);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
        Assert.False(Assert.IsType<VerificationEmailDeliveryResult>(status.Value).Sent);
    }

    [Theory]
    [InlineData("", "jan@x.example", "https://x/v?token=t")]
    [InlineData("tenant-1", "", "https://x/v?token=t")]
    [InlineData("tenant-1", "jan@x.example", "")]
    public async Task Send_RejectsIncompleteRequest_WithoutInvokingDelivery(string tenant, string email, string link)
    {
        var delivery = new Mock<IVerificationEmailDelivery>();

        var result = await new EmailVerificationDeliveryController(delivery.Object)
            .Send(new VerificationEmailDeliveryRequest(tenant, email, link), default);

        Assert.IsType<BadRequestResult>(result);
        delivery.Verify(d => d.SendAsync(It.IsAny<VerificationEmailRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
