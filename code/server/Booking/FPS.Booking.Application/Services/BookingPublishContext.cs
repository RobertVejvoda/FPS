namespace FPS.Booking.Application.Services;

public sealed record BookingPublishContext(
    string TenantId,
    string CorrelationId,
    string ActorType,
    string? ActorId,
    string? CausationId = null);
