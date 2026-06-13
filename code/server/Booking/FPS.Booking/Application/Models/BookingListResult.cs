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

// HR-safe employee history item. Same surface as HrBookingListItem but scoped
// to a single requestor — RequestorRef stays on the result envelope, not on
// every row, so the table stays narrow.
public record HrEmployeeHistoryItem(
    Guid RequestId,
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

// Summary counts span the date range and ignore the status filter, so HR
// can read the overall pattern even when drilling into one status.
public record HrEmployeeHistorySummary(
    int Total,
    int Allocated,
    int Rejected,
    int Cancelled,
    int Pending);

public record HrEmployeeHistoryResult(
    string RequestorRef,
    HrEmployeeHistorySummary Summary,
    IReadOnlyList<HrEmployeeHistoryItem> Items,
    string? NextCursor,
    int TotalCount = 0);
