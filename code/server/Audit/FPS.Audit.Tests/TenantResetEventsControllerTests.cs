using FPS.Audit.Application;
using FPS.Audit.Controllers;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPS.Audit.Tests;

public sealed class TenantResetEventsControllerTests
{
    private readonly InMemoryAuditRepository repository = new();
    private readonly TenantResetEventsController controller;

    public TenantResetEventsControllerTests()
    {
        var handler = new SandboxResetAuditHandler(repository, NullLogger<SandboxResetAuditHandler>.Instance);
        controller = new TenantResetEventsController(handler);
    }

    [Fact]
    public async Task Handle_EmptyTenantId_Returns400()
    {
        var envelope = new TenantResetEventEnvelope("started", string.Empty, "hash-1", DateTimeOffset.UtcNow, null);

        var result = await controller.Handle(envelope, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Handle_EmptyAction_Returns400()
    {
        var envelope = new TenantResetEventEnvelope(string.Empty, "tenant-1", "hash-1", DateTimeOffset.UtcNow, null);

        var result = await controller.Handle(envelope, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Handle_ValidEvent_ReturnsOkAndInvokesHandler()
    {
        var envelope = new TenantResetEventEnvelope("completed", "tenant-1", "hash-1", DateTimeOffset.UtcNow, "detail");

        var result = await controller.Handle(envelope, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        var (items, total) = await repository.QueryAsync(
            new AuditQueryRequest { EventType = "platform.sandboxReset" }, "tenant-1");
        Assert.Equal(1, total);
        Assert.Single(items);
    }

    [Fact]
    public void Controller_IsDaprInternalOnly()
    {
        var attribute = Attribute.GetCustomAttribute(
            typeof(TenantResetEventsController), typeof(DaprInternalOnlyAttribute));

        Assert.NotNull(attribute);
    }
}
