using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Audit.Tests;

public sealed class SandboxResetAuditHandlerTests
{
    private readonly Mock<IAuditRepository> repository = new();
    private readonly SandboxResetAuditHandler handler;

    public SandboxResetAuditHandlerTests()
    {
        handler = new SandboxResetAuditHandler(repository.Object, NullLogger<SandboxResetAuditHandler>.Instance);
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Theory]
    [InlineData("started")]
    [InlineData("completed")]
    [InlineData("failed")]
    public async Task Handle_ValidEvent_AppendsImmutableAuditRecord(string action)
    {
        var e = BuildEvent(action, actorHash: "already-hashed-actor");

        await handler.HandleAsync(e);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                a.EventType == "platform.sandboxReset" &&
                a.EventVersion == 1 &&
                a.TenantId == "tenant-1" &&
                a.ActorType == "system" &&
                a.Source == "platform" &&
                a.EntityType == "sandboxReset" &&
                a.EntityId == "tenant-1" &&
                a.Action == $"platform.sandboxReset.{action}" &&
                a.Result == action &&
                a.AuditRecordId != Guid.Empty &&
                !string.IsNullOrEmpty(a.Summary)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WritesExactlyOneRecord()
    {
        await handler.HandleAsync(BuildEvent("completed"));

        repository.Verify(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ActorHash_IsPassedThroughUnchanged()
    {
        var e = BuildEvent("started", actorHash: "a3f1b2c4-precomputed-hash");

        await handler.HandleAsync(e);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.ActorHash == "a3f1b2c4-precomputed-hash"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PayloadCarriesActionAndDetail()
    {
        var e = BuildEvent("completed", detail: "purged=[bookings:3] profiles=5 slots=10");

        await handler.HandleAsync(e);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                a.Payload.ToJsonString().Contains("completed") &&
                a.Payload.ToJsonString().Contains("purged=[bookings:3]")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullDetail_IsPreservedAsNullInPayload()
    {
        var e = BuildEvent("started", detail: null);

        await handler.HandleAsync(e);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.Payload["detail"] == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEvent_DoesNotAppendAgain()
    {
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.HandleAsync(BuildEvent("completed"));

        repository.Verify(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Redelivery_IsIdempotent()
    {
        // Same Action + TenantId + OccurredAt yields the same deterministic
        // sourceEventId, so the second delivery collapses onto the first.
        var occurredAt = DateTimeOffset.UtcNow;
        var repo = new InMemoryAuditRepository();
        var h = new SandboxResetAuditHandler(repo, NullLogger<SandboxResetAuditHandler>.Instance);
        var e = new TenantResetEventEnvelope("completed", "tenant-1", "hash-1", occurredAt, "detail");

        await h.HandleAsync(e);
        await h.HandleAsync(e);

        var (items, total) = await repo.QueryAsync(
            new AuditQueryRequest { EventType = "platform.sandboxReset" }, "tenant-1");

        Assert.Equal(1, total);
        Assert.Single(items);
    }

    private static TenantResetEventEnvelope BuildEvent(
        string action,
        string actorHash = "hash-1",
        string? detail = "detail") =>
        new(action, "tenant-1", actorHash, DateTimeOffset.UtcNow, detail);
}
