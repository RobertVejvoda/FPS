using System.Text.Json;
using System.Text.Json.Serialization;

namespace FPS.DataHub.Application;

public sealed record BookingEventEnvelope(
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
    BookingEventPayload Payload,
    DateTime? PublishedAt = null);

public sealed record BookingEventPayload(
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
    // DRAW009: safe lifecycle steps from booking.drawCompleted / booking.drawFailed events.
    IReadOnlyList<DrawProgressStepEnvelope>? LifecycleSteps = null,
    string? SafeFailureReason = null,
    // AUD008: vehicle and location facts captured at submission time for the auditor read model.
    string? VehicleLicensePlate = null,
    string? VehicleType = null,
    bool? VehicleIsElectric = null,
    // PLAT-seats (#710): "Parking" or "Seats" so outcome evidence distinguishes the resource type.
    string? ResourceType = null)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Safe Draw lifecycle step as received in the event envelope from Booking.
/// No seeds, stack traces, or employee-private data.
/// </summary>
public sealed record DrawProgressStepEnvelope(
    string StepName,
    string Status,
    string? Summary,
    DateTime? OccurredAt);
