using Dapr.Client;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using Moq;

namespace FPS.Audit.Tests.Infrastructure;

public sealed class DaprAuditRepositoryTests
{
    private const string StoreName = "auditstore";
    private readonly Dictionary<string, object?> store = new();

    private DaprAuditRepository BuildRepo()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<AuditRecord>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, AuditRecord, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<bool>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<List<string>>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, List<string>, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<AuditRecord>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as AuditRecord : null);

        mock.Setup(c => c.GetStateAsync<bool>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) && v is bool b && b);

        mock.Setup(c => c.GetStateAsync<List<string>>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as List<string> : null);

        mock.Setup(c => c.DeleteStateAsync(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, _, _, _) => store.Remove(key))
            .Returns(Task.CompletedTask);

        return new DaprAuditRepository(mock.Object);
    }

    private static AuditRecord MakeRecord(
        string tenantId = "demo",
        string? sourceEventId = null,
        DateTime? occurredAt = null,
        string eventType = "booking.requestSubmitted") => new()
    {
        AuditRecordId = Guid.NewGuid(),
        SourceEventId = sourceEventId ?? Guid.NewGuid().ToString(),
        EventType = eventType,
        EventVersion = 1,
        OccurredAt = occurredAt ?? DateTime.UtcNow,
        RecordedAt = DateTime.UtcNow,
        TenantId = tenantId,
        CorrelationId = Guid.NewGuid().ToString(),
        ActorType = "user",
        Source = "fairspot-booking",
        EntityType = "bookingRequest",
        Action = "submit",
    };

    // ── Restart persistence ────────────────────────────────────────────────────

    [Fact]
    public async Task AppendAsync_ThenRestart_RecordSurvives()
    {
        var repo1 = BuildRepo();
        var record = MakeRecord();
        await repo1.AppendAsync(record);

        var repo2 = BuildRepo();
        var (items, total) = await repo2.QueryAsync(new AuditQueryRequest(), "demo");

        Assert.Equal(1, total);
        Assert.Equal(record.AuditRecordId, items[0].AuditRecordId);
    }

    [Fact]
    public async Task ExistsAsync_AfterAppend_ReturnsTrue()
    {
        var repo = BuildRepo();
        var record = MakeRecord();
        await repo.AppendAsync(record);
        Assert.True(await repo.ExistsAsync(record.SourceEventId, record.TenantId));
    }

    [Fact]
    public async Task ExistsAsync_BeforeAppend_ReturnsFalse()
    {
        var repo = BuildRepo();
        Assert.False(await repo.ExistsAsync("unknown-event-id", "demo"));
    }

    // ── Idempotency ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AppendAsync_DuplicateSourceEventId_NotAppendedTwice()
    {
        var repo = BuildRepo();
        var record = MakeRecord();
        await repo.AppendAsync(record);
        // Same SourceEventId, different AuditRecordId — idempotency must reject it
        await repo.AppendAsync(MakeRecord(sourceEventId: record.SourceEventId));

        var (_, total) = await repo.QueryAsync(new AuditQueryRequest(), "demo");
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task AppendAsync_SameSourceEventId_DifferentTenants_BothAccepted()
    {
        // Idempotency key is tenant-scoped: the same upstream event ID must not
        // block a second tenant from recording their own audit evidence.
        var repo = BuildRepo();
        var sharedSourceEventId = Guid.NewGuid().ToString();
        await repo.AppendAsync(MakeRecord("tenant-a", sourceEventId: sharedSourceEventId));
        await repo.AppendAsync(MakeRecord("tenant-b", sourceEventId: sharedSourceEventId));

        var (_, aTotal) = await repo.QueryAsync(new AuditQueryRequest(), "tenant-a");
        var (_, bTotal) = await repo.QueryAsync(new AuditQueryRequest(), "tenant-b");

        Assert.Equal(1, aTotal);
        Assert.Equal(1, bTotal);
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_TenantIsolation_OnlyOwnTenantRecords()
    {
        var repo = BuildRepo();
        await repo.AppendAsync(MakeRecord("demo"));
        await repo.AppendAsync(MakeRecord("other-co"));

        var (demoItems, demoTotal) = await repo.QueryAsync(new AuditQueryRequest(), "demo");
        var (otherItems, otherTotal) = await repo.QueryAsync(new AuditQueryRequest(), "other-co");

        Assert.Equal(1, demoTotal);
        Assert.Equal(1, otherTotal);
        Assert.Equal("demo", demoItems[0].TenantId);
        Assert.Equal("other-co", otherItems[0].TenantId);
    }

    // ── Query filtering ────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_FilterByEventType_ReturnsMatching()
    {
        var repo = BuildRepo();
        await repo.AppendAsync(MakeRecord(eventType: "booking.requestSubmitted"));
        await repo.AppendAsync(MakeRecord(eventType: "booking.drawCompleted"));

        var (items, total) = await repo.QueryAsync(new AuditQueryRequest { EventType = "booking.drawCompleted" }, "demo");

        Assert.Equal(1, total);
        Assert.Equal("booking.drawCompleted", items[0].EventType);
    }

    // ── Retention ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldRecords_KeepsRecent()
    {
        var repo = BuildRepo();
        var old = MakeRecord(occurredAt: DateTime.UtcNow.AddDays(-10));
        var recent = MakeRecord(occurredAt: DateTime.UtcNow);
        await repo.AppendAsync(old);
        await repo.AppendAsync(recent);

        var cutoff = DateTime.UtcNow.AddDays(-1);
        var deleted = await repo.DeleteOlderThanAsync("demo", cutoff);

        Assert.Equal(1, deleted);
        var (items, total) = await repo.QueryAsync(new AuditQueryRequest(), "demo");
        Assert.Equal(1, total);
        Assert.Equal(recent.AuditRecordId, items[0].AuditRecordId);
    }

    [Fact]
    public async Task CountOlderThanAsync_ReturnsCorrectCount()
    {
        var repo = BuildRepo();
        await repo.AppendAsync(MakeRecord(occurredAt: DateTime.UtcNow.AddDays(-10)));
        await repo.AppendAsync(MakeRecord(occurredAt: DateTime.UtcNow.AddDays(-5)));
        await repo.AppendAsync(MakeRecord(occurredAt: DateTime.UtcNow));

        var count = await repo.CountOlderThanAsync("demo", DateTime.UtcNow.AddDays(-3));

        Assert.Equal(2, count);
    }

    // ── Tenant purge (sandbox reset) ──────────────────────────────────────────

    [Fact]
    public async Task PurgeTenantAsync_RemovesAllRecords_ReturnsCount()
    {
        var repo = BuildRepo();
        var records = new[] { MakeRecord(), MakeRecord(), MakeRecord() };
        foreach (var record in records)
            await repo.AppendAsync(record);

        var removed = await repo.PurgeTenantAsync("demo");

        Assert.Equal(3, removed);

        // Every record, dedup marker and the index key are gone.
        var (items, total) = await repo.QueryAsync(new AuditQueryRequest(), "demo");
        Assert.Equal(0, total);
        Assert.Empty(items);
        foreach (var record in records)
            Assert.False(await repo.ExistsAsync(record.SourceEventId, "demo"));
        Assert.Empty(store);
    }

    [Fact]
    public async Task PurgeTenantAsync_OnlyPurgesTargetTenant()
    {
        var repo = BuildRepo();
        await repo.AppendAsync(MakeRecord("demo"));
        await repo.AppendAsync(MakeRecord("other-co"));

        var removed = await repo.PurgeTenantAsync("demo");

        Assert.Equal(1, removed);
        var (_, demoTotal) = await repo.QueryAsync(new AuditQueryRequest(), "demo");
        var (_, otherTotal) = await repo.QueryAsync(new AuditQueryRequest(), "other-co");
        Assert.Equal(0, demoTotal);
        Assert.Equal(1, otherTotal);
    }

    [Fact]
    public async Task PurgeTenantAsync_Idempotent_SecondCallReturnsZero()
    {
        var repo = BuildRepo();
        await repo.AppendAsync(MakeRecord());

        Assert.Equal(1, await repo.PurgeTenantAsync("demo"));
        Assert.Equal(0, await repo.PurgeTenantAsync("demo"));
    }

    [Fact]
    public async Task PurgeTenantAsync_EmptyTenant_ReturnsZero()
    {
        var repo = BuildRepo();
        Assert.Equal(0, await repo.PurgeTenantAsync("never-seen"));
    }

    [Fact]
    public async Task GetRangeAsync_ReturnsRecordsInRange()
    {
        var repo = BuildRepo();
        var t0 = DateTime.UtcNow.AddDays(-5);
        await repo.AppendAsync(MakeRecord(occurredAt: t0));
        await repo.AppendAsync(MakeRecord(occurredAt: DateTime.UtcNow.AddDays(-10)));

        var range = await repo.GetRangeAsync("demo", t0.AddSeconds(-1), DateTime.UtcNow, 10);

        Assert.Single(range);
    }
}
