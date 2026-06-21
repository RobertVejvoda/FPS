namespace FPS.Booking.Application.Services;

public sealed record BookingPublishContext(
    string TenantId,
    string CorrelationId,
    string ActorType,
    string? ActorId,
    string? CausationId = null,
    // Affected employee (booking requestor) — used as Payload.RequestorId when the domain
    // event doesn't carry it directly.
    string? SubjectRequestorId = null,
    // Allocation source for booking.slotAllocated: "draw", "sameDay", "companyCarFixedSlot", "reallocation", or "manualCorrection".
    string? AllocationSource = null,
    // AUD008: request submission context for DataHub projection enrichment.
    // Carried from SubmitBookingRequestHandler so DataHub can store auditor-safe
    // requestor / vehicle / location facts without a second Profile lookup.
    string? LocationId = null,
    string? VehicleLicensePlate = null,
    string? VehicleType = null,
    bool? VehicleIsElectric = null);
