using System.Text.Json;
using System.Text.Json.Serialization;

namespace FPS.Audit.Application;

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
    BookingEventPayload Payload);

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
    string? ReallocatedFromBookingRequestId = null)
{
    // Captures any future additive payload fields not yet in the known contract.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
