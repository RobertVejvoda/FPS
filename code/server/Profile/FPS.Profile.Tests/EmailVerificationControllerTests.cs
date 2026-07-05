using FPS.Profile.Application;
using FPS.Profile.Controllers;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FPS.Profile.Tests;

// AUTH008 (#729) — the authenticated request/confirm endpoints for FairSpot-local email verification.
public sealed class EmailVerificationControllerTests
{
    private readonly Mock<IProfileRepository> profiles = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly Mock<IVerificationTokenGenerator> tokenGen = new();
    private readonly EmailVerificationController controller;

    public EmailVerificationControllerTests()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.TenantId).Returns("tenant-1");
        currentUser.Setup(u => u.UserId).Returns("user-1");
        tokenGen.Setup(g => g.Generate()).Returns("tok-123");

        var service = new EmailVerificationService(
            new InMemoryEmailVerificationRepository(), tokenGen.Object,
            new LoggingEmailVerificationSender(NullLogger<LoggingEmailVerificationSender>.Instance),
            new LoggingEmailVerificationAuditSink(NullLogger<LoggingEmailVerificationAuditSink>.Instance),
            TimeProvider.System, Options.Create(new EmailVerificationOptions()));
        controller = new EmailVerificationController(service, profiles.Object, currentUser.Object);
    }

    private void SetupProfile(string? address) =>
        profiles.Setup(p => p.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile { TenantId = "tenant-1", UserId = "user-1", Status = ProfileStatus.Active, NotificationAddress = address });

    [Fact]
    public async Task Request_WithAddress_Returns202()
    {
        SetupProfile("jan@greenlogistics.example");

        var result = await controller.Request(CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task Request_NoAddress_Returns400()
    {
        SetupProfile(null);

        var result = await controller.Request(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Request_Unauthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await controller.Request(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Confirm_ValidToken_Returns200Verified()
    {
        SetupProfile("jan@greenlogistics.example");
        await controller.Request(CancellationToken.None);

        var result = await controller.Confirm(new ConfirmEmailVerificationRequest("tok-123"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<EmailVerificationStatusResponse>(ok.Value).Verified);
    }

    [Fact]
    public async Task Confirm_InvalidToken_Returns400WithSafeReason()
    {
        SetupProfile("jan@greenlogistics.example");
        await controller.Request(CancellationToken.None);

        var result = await controller.Confirm(new ConfirmEmailVerificationRequest("wrong"), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<EmailVerificationRejectedResponse>(bad.Value);
        Assert.Equal("invalid_token", body.Reason);
        Assert.DoesNotContain("tok-123", body.Reason); // never leaks the token
    }

    [Fact]
    public async Task Confirm_MissingToken_Returns400()
    {
        var result = await controller.Confirm(new ConfirmEmailVerificationRequest(""), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Confirm_Unauthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await controller.Confirm(new ConfirmEmailVerificationRequest("tok-123"), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }
}
