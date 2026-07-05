using FPS.Profile.Application;
using FPS.Profile.Controllers;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Profile.Tests;

/// <summary>
/// Tests the internal destructive tenant-purge endpoint (PLAT003C): invalid tenant ids are
/// rejected before any deletion, and a valid purge returns the per-service removed count.
/// </summary>
public sealed class PurgeControllerTests
{
    private readonly Mock<IProfileRepository> repository = new();
    private readonly Mock<IEmailVerificationRepository> emailVerifications = new();
    private readonly PurgeController controller;

    public PurgeControllerTests()
    {
        emailVerifications.Setup(v => v.PurgeTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var purger = new ProfileTenantStorePurger(repository.Object, emailVerifications.Object);
        controller = new PurgeController(purger);
    }

    [Fact]
    public async Task PurgeTenant_InvalidTenantId_Returns400AndPurgesNothing()
    {
        var result = await controller.PurgeTenant(new TenantPurgeRequest("", SandboxReset: false), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        repository.Verify(r => r.PurgeTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeTenant_ValidTenant_ReturnsOkWithPurgeResponse()
    {
        repository.Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await controller.PurgeTenant(new TenantPurgeRequest("demo", SandboxReset: false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal("profile", response.Service);
        Assert.Equal(5, response.Count);
    }

    [Fact]
    public async Task PurgeTenant_EmptyTenant_ReturnsOkWithZeroCount()
    {
        repository.Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await controller.PurgeTenant(new TenantPurgeRequest("demo", SandboxReset: false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal(0, response.Count);
    }

    [Fact]
    public async Task PurgeTenant_AlsoPurgesEmailVerification_AndSumsCounts()
    {
        // AUTH008 (#729) — verification records must be purged with the profiles; the count is summed.
        repository.Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>())).ReturnsAsync(3);
        emailVerifications.Setup(v => v.PurgeTenantAsync("demo", It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await controller.PurgeTenant(new TenantPurgeRequest("demo", SandboxReset: false), CancellationToken.None);

        var response = Assert.IsType<TenantPurgeResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(5, response.Count);
        emailVerifications.Verify(v => v.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()), Times.Once);
    }
}

// AUTH008 (#729) — verification records are Profile-owned tenant data and must be removed on tenant
// purge / sandbox reset, tenant-scoped and idempotently.
public sealed class EmailVerificationPurgeTests
{
    private static EmailVerification Record(string tenantId, string userId) => new()
    {
        TenantId = tenantId,
        UserId = userId,
        EmailAddress = $"{userId}@{tenantId}.example",
        TokenHash = "hash",
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task PurgeTenant_RemovesOwnTenant_KeepsOthers_AndIsIdempotent()
    {
        var repo = new InMemoryEmailVerificationRepository();
        await repo.SaveAsync(Record("t1", "u1"));
        await repo.SaveAsync(Record("t1", "u2"));
        await repo.SaveAsync(Record("t2", "u1"));

        var removed = await repo.PurgeTenantAsync("t1");

        Assert.Equal(2, removed);
        Assert.Null(await repo.GetAsync("t1", "u1"));
        Assert.Null(await repo.GetAsync("t1", "u2"));
        Assert.NotNull(await repo.GetAsync("t2", "u1")); // other tenant intact

        Assert.Equal(0, await repo.PurgeTenantAsync("t1")); // idempotent repeat
    }
}
