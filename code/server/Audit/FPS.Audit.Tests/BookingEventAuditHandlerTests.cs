using FPS.Audit.Application;
using FPS.Audit.Domain;
using Moq;

namespace FPS.Audit.Tests;

public sealed class BookingEventAuditHandlerTests
{
    private readonly Mock<IAuditRepository> repository = new();
    private readonly BookingEventAuditHandler handler;

    public BookingEventAuditHandlerTests()
    {
        handler = new BookingEventAuditHandler(repository.Object);
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
        repository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
    public async Task Handle_Payload_RequestorIdIsHashed()
    {
        await handler.HandleAsync(BuildEnvelope("booking.requestSubmitted", actorId: "user-1"));

        var expectedHash = Pseudonymiser.Hash("user-1");
        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => !a.Payload.ToString()!.Contains("user-1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DrawCompleted_EntityTypeIsDrawAttempt()
    {
        var envelope = new BookingEventEnvelope(
            EventId: "event-1", EventType: "booking.drawCompleted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-1", CorrelationId: "corr-1",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(null, null, null, null, null, null, null, null, null, null));

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.EntityType == "drawAttempt"),
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
    public async Task Handle_AffectedRecipientIds_AreHashedInPayload()
    {
        var envelope = BuildEnvelope("booking.requestCancelled", actorId: "user-1",
            affectedRecipientIds: ["user-2", "user-3"]);

        await handler.HandleAsync(envelope);

        repository.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a =>
                !a.Payload.ToString()!.Contains("user-2") &&
                !a.Payload.ToString()!.Contains("user-3")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IAuditRepository_HasNoUpdateOrDeletePath()
    {
        var methods = typeof(IAuditRepository).GetMethods()
            .Select(m => m.Name.ToLowerInvariant());

        Assert.DoesNotContain(methods, m => m.Contains("update") || m.Contains("delete") || m.Contains("remove"));
    }

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
}
