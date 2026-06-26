using FPS.DataHub.Application;
using FPS.DataHub.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPS.DataHub.Tests;

file sealed class AlwaysPassHandler : IProjectionHandler
{
    public bool CanHandle(string eventType) => true;
    public Task HandleAsync(BookingEventEnvelope envelope, CancellationToken ct) => Task.CompletedTask;
}

file sealed class AlwaysFailHandler : IProjectionHandler
{
    public bool CanHandle(string eventType) => true;
    public Task HandleAsync(BookingEventEnvelope envelope, CancellationToken ct) =>
        throw new InvalidOperationException("Projection failure.");
}

file sealed class EventTypeFilterHandler(string acceptedType) : IProjectionHandler
{
    public bool CanHandle(string eventType) => eventType == acceptedType;
    public Task HandleAsync(BookingEventEnvelope envelope, CancellationToken ct) => Task.CompletedTask;
}

public sealed class EventInboxServiceTests
{
    private static DataHubDbContext CreateDb() => new(
        new DbContextOptionsBuilder<DataHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BookingEventEnvelope MakeEnvelope(
        string? eventId = null,
        string? tenantId = null,
        string eventType = "booking.requestSubmitted") => new(
        EventId: eventId ?? Guid.NewGuid().ToString(),
        EventType: eventType,
        EventVersion: 1,
        OccurredAt: DateTime.UtcNow,
        TenantId: tenantId ?? "demo",
        CorrelationId: Guid.NewGuid().ToString(),
        CausationId: null,
        ActorType: "employee",
        ActorId: null,
        Source: "booking",
        Payload: new BookingEventPayload(
            BookingRequestId: Guid.NewGuid().ToString(),
            RequestorId: Guid.NewGuid().ToString(),
            LocationId: null, Date: null, TimeSlot: null,
            PreviousStatus: null, NewStatus: "Submitted",
            ReasonCode: null, ReasonText: null, AffectedRecipientIds: null));

    // ── Idempotency ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_NewEvent_PersistsOneInboxRow()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysPassHandler()]);
        var envelope = MakeEnvelope();

        await service.AcceptAsync(envelope, CancellationToken.None);

        Assert.Equal(1, await db.EventInbox.CountAsync());
    }

    [Fact]
    public async Task Accept_DuplicateEvent_DoesNotCreateSecondRow()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysPassHandler()]);
        var envelope = MakeEnvelope();

        await service.AcceptAsync(envelope, CancellationToken.None);
        await service.AcceptAsync(envelope, CancellationToken.None);

        Assert.Equal(1, await db.EventInbox.CountAsync());
    }

    [Fact]
    public async Task Accept_DuplicateOfProcessedEvent_DoesNotReprocess()
    {
        using var db = CreateDb();
        var mock = new Mock<IProjectionHandler>();
        mock.Setup(h => h.CanHandle(It.IsAny<string>())).Returns(true);
        mock.Setup(h => h.HandleAsync(It.IsAny<BookingEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new EventInboxService(db, [mock.Object]);
        var envelope = MakeEnvelope();

        await service.AcceptAsync(envelope, CancellationToken.None);
        await service.AcceptAsync(envelope, CancellationToken.None);

        mock.Verify(h => h.HandleAsync(It.IsAny<BookingEventEnvelope>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Processing status ────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_SuccessfulHandler_MarksRowProcessed()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysPassHandler()]);
        await service.AcceptAsync(MakeEnvelope(), CancellationToken.None);

        var record = await db.EventInbox.SingleAsync();
        Assert.Equal(EventProcessingStatus.Processed, record.ProcessingStatus);
        Assert.NotNull(record.ProcessedAt);
    }

    [Fact]
    public async Task Accept_FailingHandler_MarksRowFailed()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysFailHandler()]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcceptAsync(MakeEnvelope(), CancellationToken.None));

        var record = await db.EventInbox.SingleAsync();
        Assert.Equal(EventProcessingStatus.Failed, record.ProcessingStatus);
        Assert.Equal(1, record.RetryCount);
        Assert.NotNull(record.ProcessingError);
    }

    [Fact]
    public async Task Accept_FailingHandler_AfterMaxRetries_MarksRowPoisoned()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysFailHandler()]);
        var envelope = MakeEnvelope();

        // Attempts 1 and 2 throw (status=Failed); attempt 3 reaches MaxRetries, marks Poisoned and returns
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcceptAsync(envelope, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcceptAsync(envelope, CancellationToken.None));
        await service.AcceptAsync(envelope, CancellationToken.None);

        var record = await db.EventInbox.SingleAsync();
        Assert.Equal(EventProcessingStatus.Poisoned, record.ProcessingStatus);
        Assert.Equal(3, record.RetryCount);
    }

    [Fact]
    public async Task Accept_PoisonedEvent_DoesNotRetry()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysFailHandler()]);
        var envelope = MakeEnvelope();

        // Reach Poisoned state (attempts 1–2 throw, 3rd returns)
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcceptAsync(envelope, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcceptAsync(envelope, CancellationToken.None));
        await service.AcceptAsync(envelope, CancellationToken.None);

        await service.AcceptAsync(envelope, CancellationToken.None); // 4th — Poisoned no-op

        var record = await db.EventInbox.SingleAsync();
        Assert.Equal(3, record.RetryCount);
    }

    // ── Envelope field mapping ───────────────────────────────────────────────

    [Fact]
    public async Task Accept_StoresEnvelopeFields()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysPassHandler()]);
        var eventId = Guid.NewGuid().ToString();
        var envelope = MakeEnvelope(eventId: eventId, tenantId: "acme-corp",
            eventType: "booking.drawCompleted");

        await service.AcceptAsync(envelope, CancellationToken.None);

        var record = await db.EventInbox.SingleAsync();
        Assert.Equal(eventId, record.SourceEventId);
        Assert.Equal("booking.drawCompleted", record.EventName);
        Assert.Equal("acme-corp", record.TenantId);
        Assert.Equal("booking", record.SourceService);
        Assert.Equal(1, record.EventVersion);
        Assert.NotNull(record.PayloadHash);
    }

    // ── Handler routing ──────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_OnlyMatchingHandlerInvoked()
    {
        using var db = CreateDb();
        var submitHandler = new Mock<IProjectionHandler>();
        submitHandler.Setup(h => h.CanHandle("booking.requestSubmitted")).Returns(true);
        submitHandler.Setup(h => h.HandleAsync(It.IsAny<BookingEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var drawHandler = new Mock<IProjectionHandler>();
        drawHandler.Setup(h => h.CanHandle(It.IsAny<string>())).Returns(false);

        var service = new EventInboxService(db, [submitHandler.Object, drawHandler.Object]);
        await service.AcceptAsync(MakeEnvelope(eventType: "booking.requestSubmitted"), CancellationToken.None);

        submitHandler.Verify(h => h.HandleAsync(It.IsAny<BookingEventEnvelope>(), It.IsAny<CancellationToken>()), Times.Once);
        drawHandler.Verify(h => h.HandleAsync(It.IsAny<BookingEventEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Guard on empty/missing required fields ───────────────────────────────

    [Fact]
    public async Task Accept_EmptyEventId_DoesNotPersist()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysPassHandler()]);
        var envelope = MakeEnvelope(eventId: "");

        await service.AcceptAsync(envelope, CancellationToken.None);

        Assert.Equal(0, await db.EventInbox.CountAsync());
    }

    [Fact]
    public async Task Accept_EmptyTenantId_DoesNotPersist()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysPassHandler()]);
        var envelope = MakeEnvelope(tenantId: "");

        await service.AcceptAsync(envelope, CancellationToken.None);

        Assert.Equal(0, await db.EventInbox.CountAsync());
    }

    // ── Tenant isolation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_TwoTenants_RowsAreScoped()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, [new AlwaysPassHandler()]);

        await service.AcceptAsync(MakeEnvelope(tenantId: "tenant-a"), CancellationToken.None);
        await service.AcceptAsync(MakeEnvelope(tenantId: "tenant-b"), CancellationToken.None);

        var aCount = await db.EventInbox.CountAsync(r => r.TenantId == "tenant-a");
        var bCount = await db.EventInbox.CountAsync(r => r.TenantId == "tenant-b");
        Assert.Equal(1, aCount);
        Assert.Equal(1, bCount);
    }

    // ── No handlers ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_NoHandlers_StaysPending()
    {
        using var db = CreateDb();
        var service = new EventInboxService(db, []);
        await service.AcceptAsync(MakeEnvelope(), CancellationToken.None);

        var record = await db.EventInbox.SingleAsync();
        Assert.Equal(EventProcessingStatus.Pending, record.ProcessingStatus);
    }

    // ── Cold rebuild (PERSIST005 evidence) ────────────────────────────────────
    // Demonstrates the rebuild procedure: clear inbox + projection tables, then
    // re-deliver the same events from the broker (offset reset) so projections
    // are reconstructed from durable upstream events.

    [Fact]
    public async Task ColdRebuild_ClearInboxAndProjection_ThenReplay_RebuildsDrawHistory()
    {
        using var db = CreateDb();
        var handler = new BookingProjectionHandler(db, NullLogger<BookingProjectionHandler>.Instance);
        var service = new EventInboxService(db, [handler]);

        var drawStarted = new BookingEventEnvelope(
            EventId: "evt-rebuild-draw-1",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-rebuild",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null, RequestorId: null,
                LocationId: "loc-hq", Date: "2026-07-01", TimeSlot: "08:00-17:00",
                PreviousStatus: null, NewStatus: null,
                ReasonCode: null, ReasonText: null, AffectedRecipientIds: null));

        // Initial build
        await service.AcceptAsync(drawStarted, CancellationToken.None);
        Assert.Equal(1, await db.DrawHistory.CountAsync());
        Assert.Equal(EventProcessingStatus.Processed, (await db.EventInbox.SingleAsync()).ProcessingStatus);

        // Step 1: Cold rebuild — clear inbox and projection tables
        db.DrawHistory.RemoveRange(db.DrawHistory);
        db.EventInbox.RemoveRange(db.EventInbox);
        await db.SaveChangesAsync();
        Assert.Equal(0, await db.DrawHistory.CountAsync());

        // Step 2: Re-deliver same event from broker (Dapr offset reset)
        await service.AcceptAsync(drawStarted, CancellationToken.None);

        // Projection is rebuilt from the replayed event
        Assert.Equal(1, await db.DrawHistory.CountAsync());
        var rebuilt = await db.DrawHistory.SingleAsync();
        Assert.Equal("tenant-a", rebuilt.TenantId);
        Assert.Equal("loc-hq", rebuilt.LocationId);
    }

    [Fact]
    public async Task ColdRebuild_TenantIsolation_OnlyRebuildsTargetTenant()
    {
        using var db = CreateDb();
        var handler = new BookingProjectionHandler(db, NullLogger<BookingProjectionHandler>.Instance);
        var service = new EventInboxService(db, [handler]);

        var tenantA = new BookingEventEnvelope(
            EventId: "evt-tenant-a", EventType: "booking.drawStarted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-a", CorrelationId: "c-a",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(null, null, "loc-a", "2026-07-01", "08:00-17:00", null, null, null, null, null));

        var tenantB = new BookingEventEnvelope(
            EventId: "evt-tenant-b", EventType: "booking.drawStarted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-b", CorrelationId: "c-b",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(null, null, "loc-b", "2026-07-01", "08:00-17:00", null, null, null, null, null));

        await service.AcceptAsync(tenantA, CancellationToken.None);
        await service.AcceptAsync(tenantB, CancellationToken.None);
        Assert.Equal(2, await db.DrawHistory.CountAsync());

        // Rebuild only tenant-a: clear tenant-a projections and inbox entries
        db.DrawHistory.RemoveRange(db.DrawHistory.Where(d => d.TenantId == "tenant-a"));
        db.EventInbox.RemoveRange(db.EventInbox.Where(e => e.TenantId == "tenant-a"));
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.DrawHistory.CountAsync());

        // Re-deliver only tenant-a events
        await service.AcceptAsync(tenantA, CancellationToken.None);

        Assert.Equal(2, await db.DrawHistory.CountAsync());
        Assert.Single(await db.DrawHistory.Where(d => d.TenantId == "tenant-a").ToListAsync());
        Assert.Single(await db.DrawHistory.Where(d => d.TenantId == "tenant-b").ToListAsync());
    }
}
