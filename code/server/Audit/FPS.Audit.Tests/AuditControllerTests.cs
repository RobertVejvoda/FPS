using FPS.Audit.Application;
using FPS.Audit.Controllers;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Audit.Tests;

public sealed class AuditControllerTests
{
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly InMemoryAuditRepository auditRepo = new();
    private readonly InMemoryPiiMappingRepository mappingRepo = new();
    private readonly AuditController auditController;
    private readonly PiiMappingController piiController;

    public AuditControllerTests()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.TenantId).Returns("tenant-1");
        currentUser.Setup(u => u.UserId).Returns("auditor-1");

        var queryService = new AuditQueryService(auditRepo);
        var erasureService = new PiiErasureService(mappingRepo, NullLogger<PiiErasureService>.Instance);

        auditController = new AuditController(queryService, mappingRepo, currentUser.Object);
        piiController = new PiiMappingController(erasureService, currentUser.Object);
    }

    [Fact]
    public async Task GetAudit_Unauthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await auditController.Query(new AuditQueryRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetAudit_MissingTenantId_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await auditController.Query(new AuditQueryRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetAudit_AuthenticatedAuditor_Returns200()
    {
        var result = await auditController.Query(new AuditQueryRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetAudit_ReturnsTenantScopedResults()
    {
        await auditRepo.AppendAsync(new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(), SourceEventId = "evt-1",
            EventType = "booking.requestSubmitted", EventVersion = 1,
            OccurredAt = DateTime.UtcNow, RecordedAt = DateTime.UtcNow,
            TenantId = "tenant-1", CorrelationId = "corr-1",
            ActorType = "employee", Source = "booking",
            EntityType = "bookingRequest", Payload = new()
        });

        var result = await auditController.Query(new AuditQueryRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedAuditResponse>(ok.Value);
        Assert.Equal(1, response.TotalCount);
    }

    [Fact]
    public async Task DeletePiiMapping_Unauthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await piiController.Delete("user-1", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task DeletePiiMapping_MissingTenantId_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await piiController.Delete("user-1", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task DeletePiiMapping_AuthenticatedAdmin_Returns204()
    {
        var result = await piiController.Delete("user-1", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeletePiiMapping_RemovesMappingForCallerTenant()
    {
        await mappingRepo.SaveAsync(new PiiMapping
        {
            TenantId = "tenant-1", UserId = "user-1",
            ActorHash = "hash-1"
        });

        await piiController.Delete("user-1", CancellationToken.None);

        Assert.False(await mappingRepo.ExistsAsync("user-1", "tenant-1"));
    }

    // ── Actor reference resolver (issue #482) ────────────────────────────────

    [Fact]
    public async Task ResolveActorReferences_Unauthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await auditController.ResolveActorReferences(
            new ActorReferencesRequest(["abc123"]), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ResolveActorReferences_NullList_Returns400()
    {
        var result = await auditController.ResolveActorReferences(
            new ActorReferencesRequest(null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResolveActorReferences_EmptyList_ReturnsEmptyMap()
    {
        var result = await auditController.ResolveActorReferences(
            new ActorReferencesRequest([]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ActorReferencesResponse>(ok.Value);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task ResolveActorReferences_KnownHash_ReturnsUserIdAndShortRef()
    {
        await mappingRepo.SaveAsync(new PiiMapping
        {
            TenantId = "tenant-1", UserId = "user-1",
            ActorHash = "a3f1b2c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2",
        });

        var result = await auditController.ResolveActorReferences(
            new ActorReferencesRequest(["a3f1b2c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2"]),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ActorReferencesResponse>(ok.Value);
        var item = response.Items.Single().Value;
        Assert.Equal("user-1", item.UserId);
        // Short ref must match displayActorRef in the web client: first 6 hex chars, uppercased.
        Assert.Equal("A3F1B2", item.ShortRef);
    }

    [Fact]
    public async Task ResolveActorReferences_UnknownHash_OmittedFromResponse()
    {
        var result = await auditController.ResolveActorReferences(
            new ActorReferencesRequest(["unknown-hash"]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ActorReferencesResponse>(ok.Value);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task ResolveActorReferences_CrossTenantHash_NotReturned()
    {
        // Privacy posture: mappings are tenant-scoped. An auditor in
        // tenant-1 must not see tenant-2's hash→userId resolution even if
        // they happen to know the hash.
        await mappingRepo.SaveAsync(new PiiMapping
        {
            TenantId = "tenant-2", UserId = "user-2",
            ActorHash = "shared-hash",
        });

        var result = await auditController.ResolveActorReferences(
            new ActorReferencesRequest(["shared-hash"]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ActorReferencesResponse>(ok.Value);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task ResolveActorReferences_BatchTooLarge_Returns400()
    {
        var hashes = Enumerable.Range(0, 201).Select(i => $"hash-{i}").ToArray();

        var result = await auditController.ResolveActorReferences(
            new ActorReferencesRequest(hashes), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task BookingEventAuditHandler_Ingestion_StoresPiiMapping()
    {
        // The mapping table is now populated as a side-effect of audit
        // ingestion. Without this the resolver endpoint would always
        // return empty for live records.
        var handler = new BookingEventAuditHandler(
            auditRepo, mappingRepo,
            NullLogger<BookingEventAuditHandler>.Instance);

        var envelope = new BookingEventEnvelope(
            EventId: "evt-x", EventType: "booking.requestSubmitted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-1",
            CorrelationId: "corr-1", CausationId: null,
            ActorType: "employee", ActorId: "user-7", Source: "booking",
            Payload: new BookingEventPayload("req-1", "user-7", "loc-1", "2026-06-15", "09:00-17:00",
                null, null, null, null, null));

        await handler.HandleAsync(envelope);

        Assert.True(await mappingRepo.ExistsAsync("user-7", "tenant-1"));
    }
}
