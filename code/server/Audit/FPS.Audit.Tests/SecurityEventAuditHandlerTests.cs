using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Audit.Tests;

// AUTH008B (#734) — ingestion of email-verification security evidence. The record must preserve the
// already-pseudonymised actor, carry only outcome/reason (never a token or email), and dedupe redeliveries.
public sealed class SecurityEventAuditHandlerTests
{
    private readonly Mock<IAuditRepository> repository = new();
    private readonly SecurityEventAuditHandler handler;

    public SecurityEventAuditHandlerTests()
    {
        handler = new SecurityEventAuditHandler(repository.Object, NullLogger<SecurityEventAuditHandler>.Instance);
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("succeeded")]
    [InlineData("expired")]
    [InlineData("failed")]
    public async Task Handle_AppendsSecurityAuditRecord_WithExpectedShape(string outcome)
    {
        await handler.HandleAsync(BuildEvent(outcome));

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                a.EventType == "security.email-verification" &&
                a.EventVersion == 1 &&
                a.TenantId == "tenant-1" &&
                a.ActorType == "user" &&
                a.Source == "profile" &&
                a.EntityType == "email-verification" &&
                a.Action == $"security.email-verification.{outcome}" &&
                a.Result == outcome &&
                a.AuditRecordId != Guid.Empty &&
                !string.IsNullOrEmpty(a.Summary)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ActorHash_IsStoredVerbatim_NotRehashed()
    {
        await handler.HandleAsync(BuildEvent("succeeded", actorHash: "already-hashed-actor"));

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.ActorHash == "already-hashed-actor" && a.EntityId == "already-hashed-actor"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Payload_CarriesOutcomeAndReason_ButNoTokenOrEmail()
    {
        await handler.HandleAsync(BuildEvent("failed", reason: "invalid_token"));

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                a.Payload.ToJsonString().Contains("invalid_token") &&
                a.Payload.ToJsonString().Contains("email-verification") &&
                !a.Payload.ToJsonString().Contains("@") &&
                !a.Payload.ToJsonString().Contains("token=")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullReason_IsPreservedAsNullInPayload()
    {
        await handler.HandleAsync(BuildEvent("succeeded", reason: null));

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.Payload["reason"] == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEvent_DoesNotAppendAgain()
    {
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.HandleAsync(BuildEvent("succeeded"));

        repository.Verify(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Redelivery_IsIdempotent()
    {
        // Same outcome + tenant + actor + OccurredAt yields the same deterministic sourceEventId,
        // so the redelivery collapses onto the first record.
        var occurredAt = DateTimeOffset.UtcNow;
        var repo = new InMemoryAuditRepository();
        var h = new SecurityEventAuditHandler(repo, NullLogger<SecurityEventAuditHandler>.Instance);
        var e = new SecurityEventEnvelope("email-verification", "succeeded", "tenant-1", "hash-1", occurredAt, null);

        await h.HandleAsync(e);
        await h.HandleAsync(e);

        var (items, total) = await repo.QueryAsync(
            new AuditQueryRequest { EventType = "security.email-verification" }, "tenant-1");

        Assert.Equal(1, total);
        Assert.Single(items);
    }

    private static SecurityEventEnvelope BuildEvent(
        string outcome,
        string actorHash = "hash-1",
        string? reason = "detail") =>
        new("email-verification", outcome, "tenant-1", actorHash, DateTimeOffset.UtcNow, reason);
}
