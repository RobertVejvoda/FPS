namespace FPS.Booking.Application.Workflows;

public sealed record DrawWorkflowInput(
    string TenantId,
    string LocationId,
    string Date,            // yyyy-MM-dd
    string TimeSlotStart,   // ISO8601 UTC
    string TimeSlotEnd,     // ISO8601 UTC
    string? Reason,
    string TriggerSource,   // "manual" | "scheduled" | "recovery"
    string TriggeredBy);    // user hash or scheduler identity
