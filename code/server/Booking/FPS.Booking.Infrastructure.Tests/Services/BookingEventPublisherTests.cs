using Dapr.Client;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.ValueObjects;
using FPS.Booking.Infrastructure.Services;
using Moq;

namespace FPS.Booking.Infrastructure.Tests.Services;

public sealed class BookingEventPublisherTests
{
    private const string ExpectedTopic = "booking-events";
    private const string ExpectedPubSub = "fps-pubsub";

    private readonly Mock<DaprClient> dapr = new();
    private readonly BookingDaprEventPublisher publisher;
    private readonly BookingPublishContext testCtx = new(
        TenantId: "tenant-abc",
        CorrelationId: "corr-001",
        ActorType: "employee",
        ActorId: "user-xyz");

    public BookingEventPublisherTests()
    {
        publisher = new BookingDaprEventPublisher(dapr.Object);
        dapr.Setup(d => d.PublishEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private BookingIntegrationEnvelope? Capture()
    {
        BookingIntegrationEnvelope? captured = null;
        dapr.Setup(d => d.PublishEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, BookingIntegrationEnvelope, CancellationToken>(
                (_, _, env, _) => captured = env)
            .Returns(Task.CompletedTask);
        return captured; // caller must read after await
    }

    // ── topic correctness ─────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_AlwaysUsesBookingEventsTopic()
    {
        var slot = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(8));
        var evt = new BookingRequestSubmittedEvent(BookingRequestId.New(), UserId.FromString(Guid.NewGuid().ToString()), slot);

        await publisher.WithContext(testCtx).PublishAsync(evt);

        dapr.Verify(d => d.PublishEventAsync(
            ExpectedPubSub, ExpectedTopic,
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── envelope fields ───────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_RequestSubmitted_EnvelopeHasCorrectShape()
    {
        BookingIntegrationEnvelope? captured = null;
        dapr.Setup(d => d.PublishEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, BookingIntegrationEnvelope, CancellationToken>(
                (_, _, env, _) => captured = env)
            .Returns(Task.CompletedTask);

        var slot = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(8));
        var evt = new BookingRequestSubmittedEvent(BookingRequestId.New(), UserId.FromString(Guid.NewGuid().ToString()), slot);
        await publisher.WithContext(testCtx).PublishAsync(evt);

        Assert.NotNull(captured);
        Assert.Equal("booking.requestSubmitted", captured!.EventType);
        Assert.Equal(1, captured.EventVersion);
        Assert.Equal("tenant-abc", captured.TenantId);
        Assert.Equal("corr-001", captured.CorrelationId);
        Assert.Equal("employee", captured.ActorType);
        Assert.Equal("user-xyz", captured.ActorId);
        Assert.Equal("booking", captured.Source);
        Assert.Equal(evt.EventId.ToString(), captured.EventId);
        Assert.Equal("Submitted", captured.Payload.NewStatus);
        Assert.NotNull(captured.Payload.BookingRequestId);
    }

    [Fact]
    public async Task PublishAsync_RequestRejected_EnvelopeHasCorrectEventType()
    {
        BookingIntegrationEnvelope? captured = null;
        dapr.Setup(d => d.PublishEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, BookingIntegrationEnvelope, CancellationToken>(
                (_, _, env, _) => captured = env)
            .Returns(Task.CompletedTask);

        var evt = new BookingRequestRejectedEvent(
            BookingRequestId.New(), BookingRejectionCode.DailyCapExceeded, "Cap exceeded");
        await publisher.WithContext(testCtx).PublishAsync(evt);

        Assert.NotNull(captured);
        Assert.Equal("booking.requestRejected", captured!.EventType);
        Assert.Equal("Rejected", captured.Payload.NewStatus);
        Assert.Equal("DailyCapExceeded", captured.Payload.ReasonCode);
        Assert.Equal("Cap exceeded", captured.Payload.ReasonText);
    }

    [Fact]
    public async Task PublishAsync_SlotAllocationCreated_EnvelopeHasCorrectEventType()
    {
        BookingIntegrationEnvelope? captured = null;
        dapr.Setup(d => d.PublishEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, BookingIntegrationEnvelope, CancellationToken>(
                (_, _, env, _) => captured = env)
            .Returns(Task.CompletedTask);

        var slot = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(8));
        var evt = new SlotAllocationCreatedEvent(
            SlotAllocationId.New(), BookingRequestId.New(), ParkingSlotId.FromString("slot-1"), slot);
        await publisher.WithContext(testCtx).PublishAsync(evt);

        Assert.NotNull(captured);
        Assert.Equal("booking.slotAllocated", captured!.EventType);
        Assert.Equal("Allocated", captured.Payload.NewStatus);
    }

    [Fact]
    public async Task PublishAsync_PenaltyApplied_EnvelopeHasCorrectEventType()
    {
        BookingIntegrationEnvelope? captured = null;
        dapr.Setup(d => d.PublishEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, BookingIntegrationEnvelope, CancellationToken>(
                (_, _, env, _) => captured = env)
            .Returns(Task.CompletedTask);

        var evt = new PenaltyAppliedEvent(
            BookingRequestId.New(), UserId.FromString(Guid.NewGuid().ToString()),
            PenaltyType.LateCancellation, 1, "src-1");
        await publisher.WithContext(testCtx).PublishAsync(evt);

        Assert.NotNull(captured);
        Assert.Equal("booking.penaltyApplied", captured!.EventType);
        Assert.Equal("LateCancellation", captured.Payload.ReasonCode);
    }

    // ── internal events not published ─────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_InternalPendingEvent_IsNotForwarded()
    {
        var evt = new BookingRequestPendingEvent(BookingRequestId.New());
        await publisher.WithContext(testCtx).PublishAsync(evt);

        dapr.Verify(d => d.PublishEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── draw seed not in payload ──────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_DrawStarted_SeedNotExposedInPayload()
    {
        BookingIntegrationEnvelope? captured = null;
        dapr.Setup(d => d.PublishEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, BookingIntegrationEnvelope, CancellationToken>(
                (_, _, env, _) => captured = env)
            .Returns(Task.CompletedTask);

        var timeSlot = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(8));
        var drawKey = DrawKey.Create("tenant-abc", "loc-1", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), timeSlot);
        const long seed = 12345678L;
        var evt = new DrawAttemptStartedEvent(drawKey, seed, DateTime.UtcNow);
        await publisher.WithContext(testCtx).PublishAsync(evt);

        Assert.NotNull(captured);
        Assert.Equal("booking.drawStarted", captured!.EventType);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(captured.Payload);
        Assert.DoesNotContain(seed.ToString(), payloadJson);
    }

    // ── tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_EnvelopeTenantIdMatchesContext()
    {
        BookingIntegrationEnvelope? captured = null;
        dapr.Setup(d => d.PublishEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<BookingIntegrationEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, BookingIntegrationEnvelope, CancellationToken>(
                (_, _, env, _) => captured = env)
            .Returns(Task.CompletedTask);

        var ctx = new BookingPublishContext("tenant-X", "corr", "employee", null);
        var evt = new BookingRequestCancelledEvent(BookingRequestId.New(), "User request");
        await publisher.WithContext(ctx).PublishAsync(evt);

        Assert.Equal("tenant-X", captured!.TenantId);
    }
}
