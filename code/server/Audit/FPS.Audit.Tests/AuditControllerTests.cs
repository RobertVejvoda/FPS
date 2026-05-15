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

        auditController = new AuditController(queryService, currentUser.Object);
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
}
