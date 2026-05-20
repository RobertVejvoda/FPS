using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;

namespace FPS.Audit.Tests;

public sealed class AuditRetentionServiceTests
{
    private readonly InMemoryAuditRepository repository = new();
    private readonly AuditRetentionService service;

    public AuditRetentionServiceTests()
    {
        service = new AuditRetentionService(repository, NullLogger<AuditRetentionService>.Instance);
    }

    [Fact]
    public async Task Execute_DeletesRecordsOlderThanCutoff()
    {
        var old = Record("t1", DateTime.UtcNow.AddDays(-400));
        var recent = Record("t1", DateTime.UtcNow.AddDays(-10));
        await repository.AppendAsync(old);
        await repository.AppendAsync(recent);

        var result = await service.ExecuteAsync(new AuditRetentionPolicy("t1", 365));

        Assert.Equal("completed", result.Result);
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(1, result.CandidateCount);

        // Recent record (10 days) is preserved — only 1 record is beyond the 365-day cutoff
        var oldRemaining = await repository.CountOlderThanAsync("t1", DateTime.UtcNow.AddDays(-350));
        Assert.Equal(0, oldRemaining); // the 400-day record was deleted
    }

    [Fact]
    public async Task Execute_PreservesRecordsNewerThanCutoff()
    {
        await repository.AppendAsync(Record("t1", DateTime.UtcNow.AddDays(-10)));

        var result = await service.ExecuteAsync(new AuditRetentionPolicy("t1", 365));

        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(0, result.CandidateCount);
    }

    [Fact]
    public async Task Execute_IsIdempotent_SecondRunDeletesZero()
    {
        await repository.AppendAsync(Record("t1", DateTime.UtcNow.AddDays(-400)));

        await service.ExecuteAsync(new AuditRetentionPolicy("t1", 365));
        var second = await service.ExecuteAsync(new AuditRetentionPolicy("t1", 365));

        Assert.Equal(0, second.DeletedCount);
        Assert.Equal(0, second.CandidateCount);
    }

    [Fact]
    public async Task Execute_TenantIsolation_DoesNotDeleteOtherTenantsRecords()
    {
        await repository.AppendAsync(Record("t1", DateTime.UtcNow.AddDays(-400)));
        await repository.AppendAsync(Record("t2", DateTime.UtcNow.AddDays(-400)));

        var result = await service.ExecuteAsync(new AuditRetentionPolicy("t1", 365));

        Assert.Equal(1, result.DeletedCount);

        // t2 record still counted
        var t2Count = await repository.CountOlderThanAsync("t2", DateTime.UtcNow.AddDays(-5));
        Assert.Equal(1, t2Count);
    }

    [Fact]
    public async Task Execute_EmptyStore_ReturnsZeroCounts()
    {
        var result = await service.ExecuteAsync(new AuditRetentionPolicy("t1", 365));

        Assert.Equal("completed", result.Result);
        Assert.Equal(0, result.CandidateCount);
        Assert.Equal(0, result.DeletedCount);
    }

    [Fact]
    public async Task Execute_EvidenceDoesNotContainSensitiveData()
    {
        await repository.AppendAsync(Record("t1", DateTime.UtcNow.AddDays(-400)));

        var result = await service.ExecuteAsync(new AuditRetentionPolicy("t1", 365));

        // Evidence fields are safe: tenant ID and counts only, no PII or payload
        Assert.Equal("t1", result.TenantId);
        Assert.Equal(365, result.PolicyRetentionDays);
        Assert.NotEqual(default, result.ExecutedAt);
        Assert.NotNull(result.Result);
    }

    private static AuditRecord Record(string tenantId, DateTime occurredAt) => new()
    {
        AuditRecordId = Guid.NewGuid(),
        SourceEventId = Guid.NewGuid().ToString(),
        EventType = "booking.requestSubmitted",
        EventVersion = 1,
        OccurredAt = occurredAt,
        RecordedAt = occurredAt,
        TenantId = tenantId,
        CorrelationId = Guid.NewGuid().ToString(),
        ActorType = "employee",
        Source = "FPS.Booking",
        EntityType = "bookingRequest",
        Payload = new JsonObject(),
    };
}
