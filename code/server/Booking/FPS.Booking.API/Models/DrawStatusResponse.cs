namespace FPS.Booking.API.Models;

/// <summary>
/// Employee-safe draw status response with availability summary.
/// Does not expose lottery internals, seeds, weights, or policy details.
/// </summary>
public record DrawStatusResponse(
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string DemandLevel,
    int RequestCount,
    int AvailableSpotCount,
    DateTime? NextDrawAt,
    bool CanRequest,
    string? CannotRequestReason);
