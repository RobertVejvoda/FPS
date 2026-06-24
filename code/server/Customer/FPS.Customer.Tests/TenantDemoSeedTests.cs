using FPS.Customer.Application;
using FPS.Customer.Controllers;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Customer.Tests;

public sealed class TenantDemoSeedTests
{
    private readonly InMemoryTenantRepository tenantRepo = new();
    private readonly Mock<IDemoSeedProfileClient> profileClient = new();
    private readonly Mock<IDemoSeedConfigurationClient> configClient = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly TenantDemoSeedController controller;

    public TenantDemoSeedTests()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.UserId).Returns("admin-1");

        var service = new TenantDemoSeedService(tenantRepo, profileClient.Object, configClient.Object);
        controller = new TenantDemoSeedController(service, currentUser.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        profileClient.Setup(c => c.SeedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DemoEmployeeRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((9, (string?)null));
        configClient.Setup(c => c.SeedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<DemoSlotRecord>>(), It.IsAny<DemoPolicyRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((20, (string?)null));
    }

    private async Task<string> CreateSandboxTenantAsync()
    {
        var tenant = new TenantWorkspace
        {
            TenantId = "gl-sandbox",
            Slug = "gl-sandbox",
            DisplayName = "GL Sandbox",
            Region = "EU",
            TimeZone = "Europe/Prague",
            Kind = TenantKind.Sandbox,
            Provisioning = TenantProvisioningMetadata.Generate("gl-sandbox", "gl-sandbox"),
        };
        await tenantRepo.SaveAsync(tenant, CancellationToken.None);
        return tenant.TenantId;
    }

    private async Task<string> CreateEvaluationTenantAsync()
    {
        var tenant = new TenantWorkspace
        {
            TenantId = "gl-eval",
            Slug = "gl-eval",
            DisplayName = "GL Eval",
            Region = "EU",
            TimeZone = "Europe/Prague",
            Kind = TenantKind.Evaluation,
            Provisioning = TenantProvisioningMetadata.Generate("gl-eval", "gl-eval"),
        };
        await tenantRepo.SaveAsync(tenant, CancellationToken.None);
        return tenant.TenantId;
    }

    private async Task<string> CreateProductionTenantAsync()
    {
        var tenant = new TenantWorkspace
        {
            TenantId = "prod-co",
            Slug = "prod-co",
            DisplayName = "Prod Co",
            Region = "EU",
            TimeZone = "Europe/Prague",
            Kind = TenantKind.Production,
            Provisioning = TenantProvisioningMetadata.Generate("prod-co", "prod-co"),
        };
        await tenantRepo.SaveAsync(tenant, CancellationToken.None);
        return tenant.TenantId;
    }

    [Fact]
    public async Task DemoSeed_SandboxTenant_Returns200WithSummary()
    {
        var tenantId = await CreateSandboxTenantAsync();

        var result = await controller.DemoSeed(tenantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DemoSeedResult>(ok.Value);
        Assert.Equal(tenantId, response.TenantId);
        Assert.Equal("gl-v1", response.DatasetVersion);
        Assert.Equal(9, response.ProfilesSeeded);
        Assert.Equal(20, response.SlotsSeeded);
        Assert.NotEmpty(response.GapReport);
    }

    [Fact]
    public async Task DemoSeed_EvaluationTenant_Returns200()
    {
        var tenantId = await CreateEvaluationTenantAsync();

        var result = await controller.DemoSeed(tenantId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DemoSeed_ProductionTenant_Returns400WithKindError()
    {
        var tenantId = await CreateProductionTenantAsync();

        var result = await controller.DemoSeed(tenantId, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Production", bad.Value?.ToString());
    }

    [Fact]
    public async Task DemoSeed_UnknownTenant_Returns404()
    {
        var result = await controller.DemoSeed("no-such-tenant", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DemoSeed_ProfileSeedFails_Returns400()
    {
        var tenantId = await CreateSandboxTenantAsync();
        profileClient.Setup(c => c.SeedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DemoEmployeeRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, "Profile service unreachable: connection refused"));

        var result = await controller.DemoSeed(tenantId, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Profile seed failed", bad.Value?.ToString());
    }

    [Fact]
    public async Task DemoSeed_ConfigSeedFails_Returns400()
    {
        var tenantId = await CreateSandboxTenantAsync();
        configClient.Setup(c => c.SeedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<DemoSlotRecord>>(), It.IsAny<DemoPolicyRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, "Configuration service unreachable"));

        var result = await controller.DemoSeed(tenantId, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Configuration seed failed", bad.Value?.ToString());
    }

    [Fact]
    public async Task DemoSeed_ResponseIncludesGapReport()
    {
        var tenantId = await CreateSandboxTenantAsync();

        var result = await controller.DemoSeed(tenantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DemoSeedResult>(ok.Value);
        Assert.Contains(response.GapReport, g => g.Contains("Booking"));
        Assert.Contains(response.GapReport, g => g.Contains("DataHub"));
        Assert.Contains(response.GapReport, g => g.Contains("Notification"));
    }

    [Fact]
    public async Task DemoSeed_IsIdempotent_CallsSeedTwice()
    {
        var tenantId = await CreateSandboxTenantAsync();

        await controller.DemoSeed(tenantId, CancellationToken.None);
        var result = await controller.DemoSeed(tenantId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        profileClient.Verify(c => c.SeedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DemoEmployeeRecord>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DemoSeed_UnauthenticatedUser_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);
        currentUser.Setup(u => u.UserId).Returns(string.Empty);
        var tenantId = await CreateSandboxTenantAsync();

        var result = await controller.DemoSeed(tenantId, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }
}
