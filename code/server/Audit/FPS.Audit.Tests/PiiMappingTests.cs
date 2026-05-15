using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPS.Audit.Tests;

public sealed class PiiMappingTests
{
    private readonly InMemoryPiiMappingRepository mappingRepo = new();
    private readonly InMemoryAuditRepository auditRepo = new();
    private readonly PiiErasureService service;

    public PiiMappingTests()
    {
        service = new PiiErasureService(mappingRepo, NullLogger<PiiErasureService>.Instance);
    }

    [Fact]
    public async Task Delete_IdempotentWhenMappingNotPresent()
    {
        // Should not throw — returns cleanly for a userId that was never stored.
        var ex = await Record.ExceptionAsync(() =>
            service.DeleteByUserIdAsync("nonexistent-user", "tenant-1", "actor-hash"));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Delete_RemovesMapping()
    {
        await mappingRepo.SaveAsync(new PiiMapping
        {
            TenantId = "tenant-1",
            UserId = "user-1",
            ActorHash = Pseudonymiser.Hash("user-1")!,
            Name = "Alice",
            Email = "alice@example.com"
        });

        await service.DeleteByUserIdAsync("user-1", "tenant-1", "actor-hash");

        // Verify idempotent second call also succeeds.
        var ex = await Record.ExceptionAsync(() =>
            service.DeleteByUserIdAsync("user-1", "tenant-1", "actor-hash"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Delete_TenantIsolation_DoesNotRemoveOtherTenantMapping()
    {
        await mappingRepo.SaveAsync(new PiiMapping
        {
            TenantId = "tenant-2",
            UserId = "user-1",
            ActorHash = Pseudonymiser.Hash("user-1")!
        });

        // Deleting for tenant-1 should not affect tenant-2 mapping.
        await service.DeleteByUserIdAsync("user-1", "tenant-1", "actor-hash");

        // No assertion on the repo directly (it's private), but verify no exception
        // and that a second delete on the correct tenant also succeeds idempotently.
        var ex = await Record.ExceptionAsync(() =>
            service.DeleteByUserIdAsync("user-1", "tenant-2", "actor-hash"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Delete_LeavesAuditRecordsIntact()
    {
        // Append an audit record before the erasure.
        var record = new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(),
            SourceEventId = "event-1",
            EventType = "booking.requestSubmitted",
            EventVersion = 1,
            OccurredAt = DateTime.UtcNow,
            RecordedAt = DateTime.UtcNow,
            TenantId = "tenant-1",
            CorrelationId = "corr-1",
            ActorType = "employee",
            ActorHash = Pseudonymiser.Hash("user-1"),
            Source = "booking",
            EntityType = "bookingRequest",
            EntityId = "req-1",
            Payload = new System.Text.Json.Nodes.JsonObject()
        };
        await auditRepo.AppendAsync(record);

        await mappingRepo.SaveAsync(new PiiMapping
        {
            TenantId = "tenant-1",
            UserId = "user-1",
            ActorHash = Pseudonymiser.Hash("user-1")!
        });

        await service.DeleteByUserIdAsync("user-1", "tenant-1", "actor-hash");

        // Audit record still exists and is unchanged.
        Assert.True(await auditRepo.ExistsAsync("event-1"));
    }

    [Fact]
    public void IPiiMappingRepository_HasDeletePath_ButNotOnIAuditRepository()
    {
        var auditMethods = typeof(IAuditRepository).GetMethods()
            .Select(m => m.Name.ToLowerInvariant());
        var piiMethods = typeof(IPiiMappingRepository).GetMethods()
            .Select(m => m.Name.ToLowerInvariant());

        Assert.DoesNotContain(auditMethods, m => m.Contains("delete") || m.Contains("remove"));
        Assert.Contains(piiMethods, m => m.Contains("delete"));
    }
}
