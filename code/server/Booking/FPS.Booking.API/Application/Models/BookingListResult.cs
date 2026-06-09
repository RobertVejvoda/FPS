namespace FPS.Booking.Application.Models;

public record BookingListItem(
    Guid RequestId,
    DateOnly RequestedDate,
    TimeOnly TimeSlotStart,
    TimeOnly TimeSlotEnd,
    string? LocationId,
    string Status,
    string? ReasonCode,
    string? Reason,
    string? AllocatedSlotId,
    string NextAction,
    DateTime CreatedAt,
    DateTime LastStatusChangedAt);

public record BookingListResult(
    IReadOnlyList<BookingListItem> Items,
    string? NextCursor,
    int TotalCount = 0);

// HR-safe booking item — no lottery internals, raw penalties, or candidate sequences.
public record HrBookingListItem(
    Guid RequestId,
    string RequestorRef,
    DateOnly RequestedDate,
    TimeOnly TimeSlotStart,
    TimeOnly TimeSlotEnd,
    string? LocationId,
    string Status,
    string? ReasonCode,
    string? Reason,
    string? AllocatedSlotId,
    DateTime CreatedAt,
    DateTime LastStatusChangedAt);

public record HrBookingListResult(
    IReadOnlyList<HrBookingListItem> Items,
    string? NextCursor,
    int TotalCount = 0);
