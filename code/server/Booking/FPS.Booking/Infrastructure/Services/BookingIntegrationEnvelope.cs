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
    int? WaitlistedCount = null,
    // DRAW009: safe lifecycle steps included in drawCompleted/drawFailed events
    // so DataHub can persist and expose ordered Draw progress without calling back
    // to the Booking service. Steps never include seeds, stack traces, or
    // employee-private details.
    IReadOnlyList<DrawProgressStepPayload>? LifecycleSteps = null,
    string? SafeFailureReason = null,
    // AUD008: vehicle and location context captured at submission time so DataHub
    // can store auditor-safe facts without a second Profile lookup.
    string? VehicleLicensePlate = null,
    string? VehicleType = null,
    bool? VehicleIsElectric = null);

/// <summary>
/// Safe, serialisable representation of one Draw workflow lifecycle step.
/// Excludes seeds, raw error context, and any employee-identifying detail.
/// </summary>
public sealed record DrawProgressStepPayload(
    string StepName,
    string Status,
    string? Summary,
    DateTime? OccurredAt);
