using System.Text.Json;
using System.Text.Json.Nodes;
using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Audit.Tests;

public sealed class BookingEventAuditHandlerTests
{
    private readonly Mock<IAuditRepository> repository = new();
    private readonly InMemoryPiiMappingRepository piiMappingRepository = new();
    private readonly BookingEventAuditHandler handler;

    public BookingEventAuditHandlerTests()
    {
        handler = new BookingEventAuditHandler(repository.Object, piiMappingRepository, NullLogger<BookingEventAuditHandler>.Instance);
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_ValidEvent_AppendsAuditRecord()
    {
        var envelope = BuildEnvelope("booking.requestSubmitted", actorId: "user-1");

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                a.SourceEventId == "event-1" &&
                a.EventType == "booking.requestSubmitted" &&
                a.TenantId == "tenant-1" &&
                a.CorrelationId == "corr-1" &&
                a.ActorType == "employee" &&
                a.Source == "booking" &&
                a.EntityType == "bookingRequest" &&
                a.EntityId == "req-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEvent_DoesNotAppendAgain()
    {
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", "user-1"));

        repository.Verify(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActorId_StoresHashNotRawId()
    {
        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", actorId: "user-1"));

        var expectedHash = Pseudonymiser.Hash("user-1");
        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                a.ActorHash == expectedHash &&
                a.ActorHash != "user-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullActorId_StoresNullActorHash()
    {
        var envelope = new BookingEventEnvelope(
            EventId: "event-1", EventType: "booking.drawCompleted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-1", CorrelationId: "corr-1",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(null, null, null, null, null, null, null, null, null, null));

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.ActorHash == null && a.ActorType == "system"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Payload_RequestorIdIsHashedNotRaw()
    {
        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", actorId: "user-1"));

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                !PayloadJson(a).Contains("\"user-1\"") &&
                !PayloadJson(a).Contains("requestorId")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("booking.drawStarted")]
    [InlineData("booking.drawCompleted")]
    [InlineData("booking.drawFailed")]
    public async Task Handle_DrawEvents_EntityTypeIsDrawAttempt(string eventType)
    {
        var envelope = new BookingEventEnvelope(
            EventId: "event-1", EventType: eventType, EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-1", CorrelationId: "corr-1",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(null, null, null, null, null, null, null, null, null, null));

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.EntityType == "drawAttempt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DrawEvent_WithDrawAttemptId_SetsEntityId()
    {
        var envelope = BuildEnvelopeWithExtras("booking.drawCompleted",
            extras: new Dictionary<string, JsonElement>
            {
                ["drawAttemptId"] = JsonDocument.Parse("\"draw-99\"").RootElement
            });

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.EntityId == "draw-99"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DrawEvent_WithoutDrawAttemptId_EntityIdIsNull()
    {
        var envelope = new BookingEventEnvelope(
            EventId: "event-1", EventType: "booking.drawCompleted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-1", CorrelationId: "corr-1",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(null, null, null, null, null, null, null, null, null, null));

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.EntityType == "drawAttempt" && a.EntityId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AdditivePayloadField_IsPreservedInAuditRecord()
    {
        // drawAttemptId is an event-specific additive field not in the base contract.
        // allocationId is now a named field — pass it via the explicit constructor parameter.
        var envelope = BuildEnvelopeWithExtras("booking.drawCompleted",
            extras: new Dictionary<string, JsonElement>
            {
                ["drawAttemptId"] = JsonDocument.Parse("\"draw-99\"").RootElement,
            });

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                PayloadJson(a).Contains("drawAttemptId") &&
                PayloadJson(a).Contains("draw-99")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AllocationPayloadFields_ArePresentInAuditRecord()
    {
        var envelope = BuildEnvelope("booking.slotAllocated");
        // Reconstruct with the explicit allocation fields populated
        var withAlloc = envelope with
        {
            Payload = envelope.Payload with
            {
                AllocationId = "alloc-42",
                SlotId = "slot-7",
                AllocationSource = "draw",
            }
        };

        await handler.HandleAsync(withAlloc);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                PayloadJson(a).Contains("alloc-42") &&
                PayloadJson(a).Contains("slot-7") &&
                PayloadJson(a).Contains("draw")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AffectedRecipientIds_AreHashedInPayload()
    {
        var envelope = BuildEnvelope("booking.requestCancelled", actorId: "user-1",
            affectedRecipientIds: ["user-2", "user-3"]);

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                !PayloadJson(a).Contains("user-2") &&
                !PayloadJson(a).Contains("user-3") &&
                PayloadJson(a).Contains("affectedRecipientHashes")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Pseudonymiser_Hash_IsDeterministic()
    {
        var h1 = Pseudonymiser.Hash("user-42");
        var h2 = Pseudonymiser.Hash("user-42");

        Assert.Equal(h1, h2);
        Assert.NotEqual("user-42", h1);
    }

    [Fact]
    public void Pseudonymiser_Hash_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(Pseudonymiser.Hash(null));
        Assert.Null(Pseudonymiser.Hash(string.Empty));
    }

    [Fact]
    public void IAuditRepository_HasNoUpdateOrDeletePath()
    {
        var methods = typeof(IAuditRepository).GetMethods()
            .Select(m => m.Name.ToLowerInvariant());

        Assert.DoesNotContain(methods, m => m.Contains("update") || m.Contains("delete") || m.Contains("remove"));
    }

    // ── Business activity fields (AUD006) ────────────────────────────────────

    [Theory]
    [InlineData("booking.requestSubmitted", "accepted")]
    [InlineData("booking.requestRejected",  "rejected")]
    [InlineData("booking.slotAllocated",    "allocated")]
    [InlineData("booking.requestCancelled", "cancelled")]
    [InlineData("booking.usageConfirmed",   "confirmed")]
    [InlineData("booking.drawCompleted",    "completed")]
    [InlineData("booking.requestExpired",   "expired")]
    public async Task Handle_EventType_SetsExpectedResult(string eventType, string expectedResult)
    {
        await handler.HandleAsync(BuildEnvelope(eventType));

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.Result == expectedResult),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EventType_SetsActionMatchingEventType()
    {
        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted"));

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.Action == "booking.requestSubmitted"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReasonCode_IsCarriedToAuditRecord()
    {
        var envelope = BuildEnvelope("booking.requestRejected") with
        {
            Payload = new BookingEventPayload(
                BookingRequestId: "req-1", RequestorId: "user-1",
                LocationId: "loc-1", Date: "2026-05-12", TimeSlot: "09:00-17:00",
                PreviousStatus: null, NewStatus: null,
                ReasonCode: "ProfileUnavailable", ReasonText: null,
                AffectedRecipientIds: null)
        };

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.ReasonCode == "ProfileUnavailable"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Summary_IsNonEmpty()
    {
        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted"));

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => !string.IsNullOrEmpty(a.Summary)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TraceId_IsNullWhenNoActivityContext()
    {
        // No ambient Activity.Current in unit tests — TraceId/SpanId must remain null.
        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted"));

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.TraceId == null && a.ProcessingTraceId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AuditRecordResponse field projection (AUD006) ────────────────────────

    [Fact]
    public void AuditRecordResponse_From_ExposesBusinessActivityFields()
    {
        var record = new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(),
            SourceEventId = "evt-1",
            EventType = "booking.requestSubmitted",
            EventVersion = 1,
            OccurredAt = DateTime.UtcNow,
            RecordedAt = DateTime.UtcNow,
            TenantId = "tenant-1",
            CorrelationId = "corr-1",
            ActorType = "employee",
            Source = "booking",
            EntityType = "bookingRequest",
            Action = "booking.requestSubmitted",
            Result = "accepted",
            ReasonCode = null,
            Summary = "booking.requestSubmitted on bookingRequest — accepted",
            TraceId = null,
            SpanId = null,
            ProcessingTraceId = null,
        };

        var response = AuditRecordResponse.From(record);

        Assert.Equal("booking.requestSubmitted", response.Action);
        Assert.Equal("accepted", response.Result);
        Assert.Null(response.ReasonCode);
        Assert.False(string.IsNullOrEmpty(response.Summary));
        Assert.Null(response.TraceId);
        Assert.Null(response.ProcessingTraceId);
    }

    [Fact]
    public void AuditRecordResponse_From_DoesNotExposeRawActorId()
    {
        var record = new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(), SourceEventId = "e", EventType = "booking.requestSubmitted",
            EventVersion = 1, OccurredAt = DateTime.UtcNow, RecordedAt = DateTime.UtcNow,
            TenantId = "t", CorrelationId = "c", ActorType = "employee",
            Source = "booking", EntityType = "bookingRequest",
            ActorHash = Pseudonymiser.Hash("user-99"),
            Action = "booking.requestSubmitted", Result = "accepted",
        };

        var response = AuditRecordResponse.From(record);

        Assert.NotEqual("user-99", response.ActorHash);
        Assert.Equal(Pseudonymiser.Hash("user-99"), response.ActorHash);
    }

    // ── Query filters (AUD006) ───────────────────────────────────────────────

    [Fact]
    public async Task Query_FilterByResult_ReturnsMatchingRecords()
    {
        var repo = new InMemoryAuditRepository();
        var h = new BookingEventAuditHandler(repo, new InMemoryPiiMappingRepository(), NullLogger<BookingEventAuditHandler>.Instance);

        await h.HandleAsync(BuildEnvelope("booking.requestSubmitted"));
        await h.HandleAsync(BuildEnvelope("booking.requestRejected") with { EventId = "event-2" });

        var (items, _) = await repo.QueryAsync(
            new AuditQueryRequest { Result = "rejected" }, "tenant-1");

        Assert.Single(items);
        Assert.Equal("rejected", items[0].Result);
    }

    [Fact]
    public async Task Query_FilterByAction_ReturnsMatchingRecords()
    {
        var repo = new InMemoryAuditRepository();
        var h = new BookingEventAuditHandler(repo, new InMemoryPiiMappingRepository(), NullLogger<BookingEventAuditHandler>.Instance);

        await h.HandleAsync(BuildEnvelope("booking.requestSubmitted"));
        await h.HandleAsync(BuildEnvelope("booking.slotAllocated") with { EventId = "event-2" });

        var (items, _) = await repo.QueryAsync(
            new AuditQueryRequest { Action = "booking.slotAllocated" }, "tenant-1");

        Assert.Single(items);
        Assert.Equal("booking.slotAllocated", items[0].Action);
    }

    [Fact]
    public async Task Query_FilterByReasonCode_ReturnsMatchingRecords()
    {
        var repo = new InMemoryAuditRepository();
        var h = new BookingEventAuditHandler(repo, new InMemoryPiiMappingRepository(), NullLogger<BookingEventAuditHandler>.Instance);

        var withReason = BuildEnvelope("booking.requestRejected") with
        {
            Payload = new BookingEventPayload(
                "req-1", "user-1", "loc-1", "2026-05-12", "09:00-17:00",
                null, null, "DailyCap", null, null)
        };
        await h.HandleAsync(withReason);
        await h.HandleAsync(BuildEnvelope("booking.requestSubmitted") with { EventId = "event-2" });

        var (items, _) = await repo.QueryAsync(
            new AuditQueryRequest { ReasonCode = "DailyCap" }, "tenant-1");

        Assert.Single(items);
        Assert.Equal("DailyCap", items[0].ReasonCode);
    }

    private static string PayloadJson(AuditRecord a) => a.Payload.ToJsonString();

    private static BookingEventEnvelope BuildEnvelope(
        string eventType,
        string? actorId = "user-1",
        IReadOnlyList<string>? affectedRecipientIds = null) => new(
        EventId: "event-1",
        EventType: eventType,
        EventVersion: 1,
        OccurredAt: DateTime.UtcNow,
        TenantId: "tenant-1",
        CorrelationId: "corr-1",
        CausationId: null,
        ActorType: "employee",
        ActorId: actorId,
        Source: "booking",
        Payload: new BookingEventPayload(
            BookingRequestId: "req-1",
            RequestorId: actorId,
            LocationId: "loc-1",
            Date: "2026-05-12",
            TimeSlot: "09:00-17:00",
            PreviousStatus: null,
            NewStatus: null,
            ReasonCode: null,
            ReasonText: null,
            AffectedRecipientIds: affectedRecipientIds));

    private static BookingEventEnvelope BuildEnvelopeWithExtras(
        string eventType,
        Dictionary<string, JsonElement> extras) => new(
        EventId: "event-1",
        EventType: eventType,
        EventVersion: 1,
        OccurredAt: DateTime.UtcNow,
        TenantId: "tenant-1",
        CorrelationId: "corr-1",
        CausationId: null,
        ActorType: "system",
        ActorId: null,
        Source: "booking",
        Payload: new BookingEventPayload(null, null, null, null, null, null, null, null, null, null)
        {
            AdditionalData = extras
        });
}
