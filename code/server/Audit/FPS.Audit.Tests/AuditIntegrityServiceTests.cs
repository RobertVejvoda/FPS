using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using System.Text.Json.Nodes;

namespace FPS.Audit.Tests;

public sealed class AuditIntegrityServiceTests
{
    private readonly InMemoryAuditRepository repository = new();
    private readonly AuditIntegrityService service;

    public AuditIntegrityServiceTests()
    {
        service = new AuditIntegrityService(repository);
    }

    [Fact]
    public async Task Verify_EmptyRange_ReturnsOkWithZeroCount()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        var result = await service.VerifyAsync("t1", from, to);

        Assert.Equal("ok", result.Result);
        Assert.Equal(0, result.RecordCount);
        Assert.False(result.HasMismatch);
        Assert.NotEmpty(result.IntegrityHash);
    }

    [Fact]
    public async Task Verify_SameDataCalledTwice_ProducesSameHash()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        await Append("t1", DateTime.UtcNow.AddDays(-1));
        await Append("t1", DateTime.UtcNow.AddDays(-2));

        var first = await service.VerifyAsync("t1", from, to);
        var second = await service.VerifyAsync("t1", from, to);

        Assert.Equal(first.IntegrityHash, second.IntegrityHash);
    }

    [Fact]
    public async Task Verify_ExpectedHashMatches_HasMismatchIsFalse()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        await Append("t1", DateTime.UtcNow.AddDays(-1));

        var baseline = await service.VerifyAsync("t1", from, to);
        var recheck = await service.VerifyAsync("t1", from, to, expectedHash: baseline.IntegrityHash);

        Assert.False(recheck.HasMismatch);
        Assert.Equal("ok", recheck.Result);
    }

    [Fact]
    public async Task Verify_ExpectedHashDoesNotMatch_HasMismatchIsTrue()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        await Append("t1", DateTime.UtcNow.AddDays(-1));

        var result = await service.VerifyAsync("t1", from, to, expectedHash: "00000000deadbeef");

        Assert.True(result.HasMismatch);
        Assert.Equal("mismatch", result.Result);
        Assert.Equal(1, result.MismatchCount);
    }

    [Fact]
    public async Task Verify_TenantIsolation_DoesNotIncludeOtherTenantRecords()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        await Append("t1", DateTime.UtcNow.AddDays(-1));
        await Append("t2", DateTime.UtcNow.AddDays(-1));

        var t1Result = await service.VerifyAsync("t1", from, to);
        var t2Result = await service.VerifyAsync("t2", from, to);

        Assert.Equal(1, t1Result.RecordCount);
        Assert.Equal(1, t2Result.RecordCount);
        // Different tenants produce different hashes even with same record count
        Assert.NotEqual(t1Result.IntegrityHash, t2Result.IntegrityHash);
    }

    [Fact]
    public async Task Export_ContainsSafeFieldsOnly_NoPayload()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        await Append("t1", DateTime.UtcNow.AddDays(-1));

        var records = await service.ExportAsync("t1", from, to);

        Assert.Single(records);
        var r = records[0];
        Assert.NotEqual(default, r.AuditRecordId);
        Assert.NotEmpty(r.EventType);
        Assert.NotEmpty(r.CorrelationId);
        // No Payload field — export record type has no raw payload property
        Assert.IsType<AuditExportRecord>(r);
    }

    [Fact]
    public async Task Export_TenantIsolation_ReturnsOnlyOwnTenantRecords()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        await Append("t1", DateTime.UtcNow.AddDays(-1));
        await Append("t2", DateTime.UtcNow.AddDays(-1));

        var t1Export = await service.ExportAsync("t1", from, to);

        Assert.All(t1Export, r => Assert.DoesNotContain("t2", r.CorrelationId));
        Assert.Single(t1Export);
    }

    private async Task Append(string tenantId, DateTime occurredAt)
    {
        await repository.AppendAsync(new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(),
            SourceEventId = Guid.NewGuid().ToString(),
            EventType = "booking.requestSubmitted",
            EventVersion = 1,
            OccurredAt = occurredAt,
            RecordedAt = occurredAt,
            TenantId = tenantId,
            CorrelationId = $"corr-{tenantId}-{Guid.NewGuid():N}",
            ActorType = "employee",
            Source = "FPS.Booking",
            EntityType = "bookingRequest",
            Payload = new JsonObject(),
        });
    }
}
