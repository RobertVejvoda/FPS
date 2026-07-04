using FPS.Profile.Application;
using FPS.Profile.Controllers;
using FPS.Profile.Domain;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Profile.Tests;

// NOTIF #728 — internal recipient resolution: only Active profiles with a well-formed
// NotificationAddress from a trusted provisioning source resolve to a delivery address; everything
// else fails closed. Tenant-scoped so a (tenant, userId) pair never crosses tenants.
public sealed class NotificationRecipientControllerTests
{
    private readonly Mock<IProfileRepository> repository = new();
    private readonly NotificationRecipientController controller;

    public NotificationRecipientControllerTests() => controller = new NotificationRecipientController(repository.Object);

    private static UserProfile Profile(
        string? notificationAddress = "jan.novak@greenlogistics.example",
        string factSource = "sso-claims",
        ProfileStatus status = ProfileStatus.Active) => new()
    {
        TenantId = "tenant-1",
        UserId = "user-1",
        Status = status,
        NotificationAddress = notificationAddress,
        FactSource = factSource,
    };

    private async Task<NotificationRecipientResult> ResolveAsync(string tenantId = "tenant-1", string userId = "user-1")
    {
        var result = await controller.Resolve(new NotificationRecipientRequest(tenantId, userId), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<NotificationRecipientResult>(ok.Value);
    }

    [Theory]
    [InlineData("sso-claims")]
    [InlineData("admin-seed")]
    [InlineData("admin-entry")]
    [InlineData("hr-import")]  // real HR CSV import value (HrImportService)
    [InlineData("file-import")] // real bootstrap import value (EmployeeBootstrapController)
    public async Task Resolve_VerifiedTrustedSource_ReturnsEmail(string factSource)
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(factSource: factSource));

        var result = await ResolveAsync();

        Assert.True(result.Resolved);
        Assert.Equal("jan.novak@greenlogistics.example", result.Email);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData("self-registered")] // FairSpot-local self-verification is #729, out of scope
    [InlineData("demo-seed")]       // synthetic showcase data — intentionally not trusted
    public async Task Resolve_UntrustedSource_FailsClosed(string factSource)
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(factSource: factSource));

        var result = await ResolveAsync();

        Assert.False(result.Resolved);
        Assert.Null(result.Email);
        Assert.Equal("email_unverified_source", result.Reason);
    }

    [Fact]
    public async Task Resolve_MissingAddress_FailsClosed()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(notificationAddress: null));

        var result = await ResolveAsync();

        Assert.False(result.Resolved);
        Assert.Equal("no_verified_email", result.Reason);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("two words@x.com")]
    [InlineData("@nope")]
    public async Task Resolve_MalformedAddress_FailsClosed(string malformed)
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(notificationAddress: malformed));

        var result = await ResolveAsync();

        Assert.False(result.Resolved);
        Assert.Equal("email_malformed", result.Reason);
    }

    [Fact]
    public async Task Resolve_InactiveProfile_FailsClosed()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(status: ProfileStatus.Suspended));

        var result = await ResolveAsync();

        Assert.False(result.Resolved);
        Assert.Equal("recipient_not_found", result.Reason);
    }

    [Fact]
    public async Task Resolve_UnknownRecipient_FailsClosed()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await ResolveAsync(userId: "missing");

        Assert.False(result.Resolved);
        Assert.Equal("recipient_not_found", result.Reason);
    }

    [Fact]
    public async Task Resolve_IsTenantScoped()
    {
        // A profile exists under tenant-1 but the request is for tenant-2 → no cross-tenant resolution.
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile());
        repository.Setup(r => r.GetAsync("tenant-2", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await ResolveAsync(tenantId: "tenant-2");

        Assert.False(result.Resolved);
        repository.Verify(r => r.GetAsync("tenant-2", "user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Resolve_InvalidRequest_FailsClosed()
    {
        var result = await ResolveAsync(tenantId: "", userId: "");

        Assert.False(result.Resolved);
        Assert.Equal("invalid_request", result.Reason);
    }
}
