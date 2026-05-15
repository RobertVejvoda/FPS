using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using System.Text.Json.Nodes;

namespace FPS.Audit.Tests;

public sealed class AuditQueryServiceTests
{
    private readonly InMemoryAuditRepository repository = new();
    private readonly AuditQueryService service;

    public AuditQueryServiceTests()
    {
        service = new AuditQueryService(repository);
    }

    [Fact]
    public async Task Query_NoFilters_ReturnsAllTenantRecords()
    {
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1");
        await AppendRecord("t1", "booking.slotAllocated", "bookingRequest", "req-2");

        var result = await service.QueryAsync(new AuditQueryRequest(), "t1");

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Query_TenantIsolation_ExcludesOtherTenantRecords()
    {
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1");
        await AppendRecord("t2", "booking.requestSubmitted", "bookingRequest", "req-2");

        var result = await service.QueryAsync(new AuditQueryRequest(), "t1");

        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, r => Assert.Equal("booking.requestSubmitted", r.EventType));
    }

    [Fact]
    public async Task Query_FilterBy_EntityType_ReturnsMatchingOnly()
    {
        await AppendRecord("t1", "booking.drawCompleted", "drawAttempt", "draw-1");
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1");

        var result = await service.QueryAsync(new AuditQueryRequest { EntityType = "drawAttempt" }, "t1");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("drawAttempt", result.Items[0].EntityType);
    }

    [Fact]
    public async Task Query_FilterBy_EntityId_ReturnsMatchingOnly()
    {
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1");
        await AppendRecord("t1", "booking.slotAllocated", "bookingRequest", "req-2");

        var result = await service.QueryAsync(new AuditQueryRequest { EntityId = "req-1" }, "t1");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("req-1", result.Items[0].EntityId);
    }

    [Fact]
    public async Task Query_FilterBy_EventType_ReturnsMatchingOnly()
    {
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1");
        await AppendRecord("t1", "booking.slotAllocated", "bookingRequest", "req-2");

        var result = await service.QueryAsync(new AuditQueryRequest { EventType = "booking.slotAllocated" }, "t1");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("booking.slotAllocated", result.Items[0].EventType);
    }

    [Fact]
    public async Task Query_FilterBy_ActorHash_ReturnsMatchingOnly()
    {
        var hash1 = Pseudonymiser.Hash("user-1")!;
        var hash2 = Pseudonymiser.Hash("user-2")!;

        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1", actorHash: hash1);
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-2", actorHash: hash2);

        var result = await service.QueryAsync(new AuditQueryRequest { ActorHash = hash1 }, "t1");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(hash1, result.Items[0].ActorHash);
    }

    [Fact]
    public async Task Query_FilterBy_OccurredAfter_ExcludesEarlierRecords()
    {
        var cutoff = DateTime.UtcNow;
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1",
            occurredAt: cutoff.AddMinutes(-10));
        await AppendRecord("t1", "booking.slotAllocated", "bookingRequest", "req-2",
            occurredAt: cutoff.AddMinutes(10));

        var result = await service.QueryAsync(new AuditQueryRequest { OccurredAfter = cutoff }, "t1");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("booking.slotAllocated", result.Items[0].EventType);
    }

    [Fact]
    public async Task Query_FilterBy_OccurredBefore_ExcludesLaterRecords()
    {
        var cutoff = DateTime.UtcNow;
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1",
            occurredAt: cutoff.AddMinutes(-10));
        await AppendRecord("t1", "booking.slotAllocated", "bookingRequest", "req-2",
            occurredAt: cutoff.AddMinutes(10));

        var result = await service.QueryAsync(new AuditQueryRequest { OccurredBefore = cutoff }, "t1");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("booking.requestSubmitted", result.Items[0].EventType);
    }

    [Fact]
    public async Task Query_Pagination_ReturnsCorrectPage()
    {
        for (var i = 1; i <= 7; i++)
            await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", $"req-{i}",
                occurredAt: DateTime.UtcNow.AddMinutes(-i));

        var page1 = await service.QueryAsync(new AuditQueryRequest { Page = 1, PageSize = 3 }, "t1");
        var page2 = await service.QueryAsync(new AuditQueryRequest { Page = 2, PageSize = 3 }, "t1");
        var page3 = await service.QueryAsync(new AuditQueryRequest { Page = 3, PageSize = 3 }, "t1");

        Assert.Equal(7, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(3, page2.Items.Count);
        Assert.Single(page3.Items);
    }

    [Fact]
    public async Task Query_PageSizeCappedAt100()
    {
        for (var i = 1; i <= 10; i++)
            await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", $"req-{i}");

        var result = await service.QueryAsync(new AuditQueryRequest { PageSize = 999 }, "t1");

        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task Query_ResponseShape_ContainsNoRawIds()
    {
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1");

        var result = await service.QueryAsync(new AuditQueryRequest(), "t1");

        var payloadJson = result.Items[0].Payload.GetRawText();
        Assert.DoesNotContain("requestorId", payloadJson);
        Assert.DoesNotContain("actorId", payloadJson);
    }

    [Fact]
    public async Task Query_ResponseShape_DoesNotIncludeTenantId()
    {
        await AppendRecord("t1", "booking.requestSubmitted", "bookingRequest", "req-1");

        var result = await service.QueryAsync(new AuditQueryRequest(), "t1");

        // TenantId is absent from AuditRecordResponse per the response contract
        var props = typeof(AuditRecordResponse).GetProperties()
            .Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain("tenantid", props);
    }

    private async Task AppendRecord(
        string tenantId, string eventType, string entityType, string entityId,
        string? actorHash = null, DateTime? occurredAt = null)
    {
        var record = new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(),
            SourceEventId = Guid.NewGuid().ToString(),
            EventType = eventType,
            EventVersion = 1,
            OccurredAt = occurredAt ?? DateTime.UtcNow,
            RecordedAt = DateTime.UtcNow,
            TenantId = tenantId,
            CorrelationId = "corr-1",
            ActorType = "employee",
            ActorHash = actorHash ?? Pseudonymiser.Hash("user-1"),
            Source = "booking",
            EntityType = entityType,
            EntityId = entityId,
            Payload = new System.Text.Json.Nodes.JsonObject()
        };
        await repository.AppendAsync(record);
    }
}
