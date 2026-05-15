using FPS.Reporting.Application;
using FPS.Reporting.Controllers;
using FPS.Reporting.Domain;
using FPS.Reporting.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Reporting.Tests;

public sealed class ReportingControllerTests
{
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly InMemoryReportingRepository repository = new();
    private readonly ReportingController controller;

    public ReportingControllerTests()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.TenantId).Returns("tenant-1");

        var queryService = new ReportingQueryService(repository);
        controller = new ReportingController(queryService, currentUser.Object);
    }

    [Fact]
    public async Task GetSummary_Unauthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await controller.GetSummary(new ReportingQueryRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetSummary_MissingTenantId_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await controller.GetSummary(new ReportingQueryRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetSummary_AuthenticatedReportViewer_Returns200()
    {
        var result = await controller.GetSummary(new ReportingQueryRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetSummary_ReturnsTenantScopedResults()
    {
        await repository.ApplyMetricsAsync("tenant-1", "2026-06-01", "loc-1", "09:00-17:00",
            m => m.IncrementDemand());
        await repository.ApplyMetricsAsync("tenant-2", "2026-06-01", "loc-1", "09:00-17:00",
            m => m.IncrementDemand());

        var result = await controller.GetSummary(new ReportingQueryRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ParkingSummaryResponse>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal("loc-1", response.Items[0].LocationId);
    }

    [Fact]
    public async Task GetFairness_Unauthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await controller.GetFairness(new FairnessQueryRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetFairness_MissingTenantId_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await controller.GetFairness(new FairnessQueryRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetFairness_AuthenticatedReportViewer_Returns200()
    {
        var result = await controller.GetFairness(new FairnessQueryRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetFairness_ReturnsTenantScopedResults()
    {
        await repository.ApplyFairnessAsync("tenant-1", "hash-u1", "2026-06-01", "loc-1",
            f => f.IncrementRequest());
        await repository.ApplyFairnessAsync("tenant-2", "hash-u2", "2026-06-01", "loc-1",
            f => f.IncrementRequest());

        var result = await controller.GetFairness(new FairnessQueryRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FairnessResponse>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal("hash-u1", response.Items[0].RequestorHash);
    }
}
