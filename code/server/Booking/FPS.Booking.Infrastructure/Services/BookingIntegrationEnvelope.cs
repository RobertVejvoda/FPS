namespace FPS.Booking.Infrastructure.Services;

// Publisher-side envelope — serializes to the same JSON shape that Notification,
// Audit, and Reporting consumers expect on the "booking-events" topic.
public sealed record BookingIntegrationEnvelope(
    string EventId,
    string EventType,
    int EventVersion,
    DateTime OccurredAt,
    string TenantId,
    string CorrelationId,
    string? CausationId,
    string ActorType,
    string? ActorId,
    string Source,
    BookingIntegrationPayload Payload);

public sealed record BookingIntegrationPayload(
    string? BookingRequestId,
    string? RequestorId,
    string? LocationId,
    string? Date,
    string? TimeSlot,
    string? PreviousStatus,
    string? NewStatus,
    string? ReasonCode,
    string? ReasonText,
    IReadOnlyList<string>? AffectedRecipientIds,
    string? AllocationId = null,
    string? SlotId = null,
    string? AllocationSource = null,
    string? ReallocatedFromBookingRequestId = null,
    string? DrawAttemptId = null,
    int? AllocatedCount = null,
    int? RejectedCount = null,
    int? WaitlistedCount = null);
