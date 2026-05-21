using Dapr.Client;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Events;
using FPS.SharedKernel.DomainEvents;

namespace FPS.Booking.Infrastructure.Services;

public sealed class BookingDaprEventPublisher(DaprClient daprClient) : IBookingEventPublisher
{
    private const string PubSubName = "fps-pubsub";
    private const string Topic = "booking-events";

    // Returns a contextual publisher that wraps domain events in the integration envelope.
    public IEventPublisher WithContext(BookingPublishContext context) =>
        new ContextualEventPublisher(daprClient, context);

    // Fallback: publish without caller-supplied context (system actor, no tenantId).
    // Used when domain raises events outside an identified request context.
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        var fallback = new BookingPublishContext(
            TenantId: string.Empty,
            CorrelationId: domainEvent.EventId.ToString(),
            ActorType: "system",
            ActorId: null);
        return new ContextualEventPublisher(daprClient, fallback)
            .PublishAsync(domainEvent, cancellationToken);
    }

    private sealed class ContextualEventPublisher(DaprClient daprClient, BookingPublishContext ctx) : IEventPublisher
    {
        public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
            where TEvent : IDomainEvent
        {
            var envelope = MapToEnvelope(domainEvent);
            if (envelope is null) return;
            await daprClient.PublishEventAsync(PubSubName, Topic, envelope, cancellationToken);
        }

        private BookingIntegrationEnvelope? MapToEnvelope<TEvent>(TEvent evt) where TEvent : IDomainEvent
        {
            // SubjectRequestorId from context is the affected booking requestor — used as
            // Payload.RequestorId when the domain event doesn't carry it directly.
            var requestorId = ctx.SubjectRequestorId;

            BookingIntegrationPayload? payload = evt switch
            {
                BookingRequestSubmittedEvent e => new(
                    BookingRequestId: e.RequestId.Value.ToString(),
                    RequestorId: e.RequestorId.Value.ToString(),
                    LocationId: null,
                    Date: e.RequestedPeriod.Start.ToString("yyyy-MM-dd"),
                    TimeSlot: $"{e.RequestedPeriod.Start:HH:mm}-{e.RequestedPeriod.End:HH:mm}",
                    PreviousStatus: null,
                    NewStatus: "Submitted",
                    ReasonCode: null, ReasonText: null, AffectedRecipientIds: null),

                BookingRequestRejectedEvent e => new(
                    BookingRequestId: e.RequestId.Value.ToString(),
                    RequestorId: requestorId,
                    LocationId: null, Date: null, TimeSlot: null,
                    PreviousStatus: null,
                    NewStatus: "Rejected",
                    ReasonCode: e.RejectionCode.ToString(),
                    ReasonText: e.Reason,
                    AffectedRecipientIds: null),

                BookingRequestCancelledEvent e => new(
                    BookingRequestId: e.RequestId.Value.ToString(),
                    RequestorId: requestorId,
                    LocationId: null, Date: null, TimeSlot: null,
                    PreviousStatus: null,
                    NewStatus: "Cancelled",
                    ReasonCode: null,
                    ReasonText: e.Reason,
                    AffectedRecipientIds: null),

                SlotAllocationCreatedEvent e => new(
                    BookingRequestId: e.RequestId.Value.ToString(),
                    RequestorId: requestorId,
                    LocationId: null,
                    Date: e.Period.Start.ToString("yyyy-MM-dd"),
                    TimeSlot: $"{e.Period.Start:HH:mm}-{e.Period.End:HH:mm}",
                    PreviousStatus: null,
                    NewStatus: "Allocated",
                    ReasonCode: null, ReasonText: null, AffectedRecipientIds: null,
                    AllocationId: e.AllocationId.Value.ToString(),
                    SlotId: e.SlotId.Value.ToString(),
                    AllocationSource: ctx.AllocationSource ?? "unknown"),

                BookingRequestReallocatedEvent e => new(
                    BookingRequestId: e.NewRequestId.Value.ToString(),
                    RequestorId: e.NewRequestorId.Value.ToString(),
                    LocationId: null, Date: null, TimeSlot: null,
                    PreviousStatus: null,
                    NewStatus: "Allocated",
                    ReasonCode: null, ReasonText: null,
                    AffectedRecipientIds: [e.OriginalCancelledRequestId.Value.ToString()],
                    AllocationId: null,
                    SlotId: e.SlotId.Value.ToString(),
                    AllocationSource: "reallocation",
                    ReallocatedFromBookingRequestId: e.OriginalCancelledRequestId.Value.ToString()),

                DrawAttemptStartedEvent e => new(
                    BookingRequestId: null,
                    RequestorId: null,
                    LocationId: e.DrawKey.LocationId,
                    Date: e.DrawKey.Date.ToString("yyyy-MM-dd"),
                    TimeSlot: $"{e.DrawKey.TimeSlot.Start:HH:mm}-{e.DrawKey.TimeSlot.End:HH:mm}",
                    PreviousStatus: null, NewStatus: null,
                    ReasonCode: null, ReasonText: null, AffectedRecipientIds: null),

                DrawAttemptCompletedEvent e => new(
                    BookingRequestId: null,
                    RequestorId: null,
                    LocationId: e.DrawKey.LocationId,
                    Date: e.DrawKey.Date.ToString("yyyy-MM-dd"),
                    TimeSlot: $"{e.DrawKey.TimeSlot.Start:HH:mm}-{e.DrawKey.TimeSlot.End:HH:mm}",
                    PreviousStatus: null, NewStatus: null,
                    ReasonCode: null, ReasonText: null, AffectedRecipientIds: null),

                PenaltyAppliedEvent e => new(
                    BookingRequestId: e.RequestId.Value.ToString(),
                    RequestorId: e.RequestorId.Value.ToString(),
                    LocationId: null, Date: null, TimeSlot: null,
                    PreviousStatus: null, NewStatus: null,
                    ReasonCode: e.PenaltyType.ToString(),
                    ReasonText: null, AffectedRecipientIds: null),

                BookingRequestNoShowEvent e => new(
                    BookingRequestId: e.RequestId.Value.ToString(),
                    RequestorId: requestorId,
                    LocationId: null, Date: null, TimeSlot: null,
                    PreviousStatus: null,
                    NewStatus: "NoShow",
                    ReasonCode: null, ReasonText: null, AffectedRecipientIds: null),

                BookingRequestUsedEvent e => new(
                    BookingRequestId: e.RequestId.Value.ToString(),
                    RequestorId: requestorId,
                    LocationId: null, Date: null, TimeSlot: null,
                    PreviousStatus: null,
                    NewStatus: "Used",
                    ReasonCode: null, ReasonText: null, AffectedRecipientIds: null),

                ManualCorrectionAppliedEvent e => new(
                    BookingRequestId: e.RequestId.Value.ToString(),
                    RequestorId: requestorId,
                    LocationId: null, Date: null, TimeSlot: null,
                    PreviousStatus: e.OldValue,
                    NewStatus: e.NewValue,
                    ReasonCode: e.CorrectionType,
                    ReasonText: e.Reason,
                    AffectedRecipientIds: null),

                // Internal-only events — not published as integration events.
                BookingRequestPendingEvent => null,
                BookingRequestAllocatedEvent => null,
                SlotAllocationConfirmedEvent => null,
                SlotAllocationExpiredEvent => null,
                SlotAllocationCancelledEvent => null,
                SlotUsageStartedEvent => null,
                SlotUsageCompletedEvent => null,
                DrawRunEvent => null,

                _ => null,
            };

            if (payload is null) return null;

            var eventType = evt switch
            {
                BookingRequestSubmittedEvent => "booking.requestSubmitted",
                BookingRequestRejectedEvent => "booking.requestRejected",
                BookingRequestCancelledEvent => "booking.requestCancelled",
                SlotAllocationCreatedEvent => "booking.slotAllocated",
                BookingRequestReallocatedEvent => "booking.slotAllocated",
                DrawAttemptStartedEvent => "booking.drawStarted",
                DrawAttemptCompletedEvent => "booking.drawCompleted",
                PenaltyAppliedEvent => "booking.penaltyApplied",
                BookingRequestNoShowEvent => "booking.noShowRecorded",
                BookingRequestUsedEvent => "booking.usageConfirmed",
                ManualCorrectionAppliedEvent => "booking.manualCorrectionApplied",
                _ => null,
            };

            if (eventType is null) return null;

            return new BookingIntegrationEnvelope(
                EventId: evt.EventId.ToString(),
                EventType: eventType,
                EventVersion: 1,
                OccurredAt: evt.OccurredOn.UtcDateTime,
                TenantId: ctx.TenantId,
                CorrelationId: ctx.CorrelationId,
                CausationId: ctx.CausationId,
                ActorType: ctx.ActorType,
                ActorId: ctx.ActorId,
                Source: "booking",
                Payload: payload);
        }
    }
}
