using FPS.Reporting.Controllers;
using FPS.Reporting.Domain;
using FPS.Reporting.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Reporting.Tests;

public sealed class ErasureControllerTests
{
    private readonly InMemoryReportingRepository repository = new();
    private readonly ErasureController controller;

    public ErasureControllerTests()
    {
        controller = new ErasureController(repository);
    }

    [Fact]
    public async Task Erase_PrefersTargetUserId_OverTargetActorHash()
    {
        // After #474 Reporting stores raw RequestorRef, not a hash. The
        // controller must prefer TargetUserId (Audit's ErasureWorkflow already
        // forwards it) so a real cross-service erasure matches actual rows.
        await repository.ApplyFairnessAsync("t1", "user-1", "2026-06-01", "loc-1",
            f => f.IncrementRequest());

        var input = new ServiceErasureInput(
            ErasureRequestId: "req-1",
            TenantId: "t1",
            TargetActorHash: "irrelevant-legacy-hash",
            TargetUserId: "user-1");

        var result = await controller.Erase(input, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ServiceErasureResult>(ok.Value);
        Assert.Equal("anonymised", payload.Treatment);
        Assert.Equal(1, payload.AffectedCount);
    }

    [Fact]
    public async Task Erase_NoTargetUserId_FallsBackToTargetActorHashButMatchesNothing()
    {
        // Legacy callers that only send TargetActorHash never match rows in
        // the post-#474 shape (which are stored under raw user ids). The
        // controller still accepts the request shape — it just reports
        // notApplicable, which is the right answer.
        await repository.ApplyFairnessAsync("t1", "user-1", "2026-06-01", "loc-1",
            f => f.IncrementRequest());

        var input = new ServiceErasureInput(
            ErasureRequestId: "req-1",
            TenantId: "t1",
            TargetActorHash: "legacy-hash-of-user-1");

        var result = await controller.Erase(input, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ServiceErasureResult>(ok.Value);
        Assert.Equal("notApplicable", payload.Treatment);
        Assert.Equal(0, payload.AffectedCount);
    }

    [Fact]
    public async Task Erase_NoTargetAtAll_ReturnsNotApplicable()
    {
        var input = new ServiceErasureInput(
            ErasureRequestId: "req-1",
            TenantId: "t1",
            TargetActorHash: string.Empty);

        var result = await controller.Erase(input, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ServiceErasureResult>(ok.Value);
        Assert.Equal("notApplicable", payload.Treatment);
        Assert.Equal(0, payload.AffectedCount);
    }
}
