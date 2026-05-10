namespace FPS.Booking.Application.Models;

public record BookingListItem(
    Guid RequestId,
    DateOnly RequestedDate,
    TimeOnly TimeSlotStart,
    TimeOnly TimeSlotEnd,
    string? LocationId,
    string Status,
    string? Reason,
    string? AllocatedSlotId,
    string NextAction,
    DateTime CreatedAt,
    DateTime LastStatusChangedAt);

public record BookingListResult(
    IReadOnlyList<BookingListItem> Items,
    string? NextCursor);
