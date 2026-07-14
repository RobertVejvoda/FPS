namespace FPS.Configuration.Domain;

/// <summary>
/// SEAT001 (#783) — a named area of the tenant's seat grid at one location. Areas group seats
/// and carry team ownership for the future Seats Draw: the owning team's requests get first
/// priority for the area's seats, leftovers open to other teams (allocation-rules.md,
/// "Seats Allocation Extension"). Ownership is a plain team-vocabulary string matching the
/// Profile/HR roster facts that will carry team membership (SEAT002) — Configuration does not
/// own a team registry.
/// </summary>
public sealed record SeatArea
{
    public string AreaId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    // Employee-safe display name (e.g. "Team Area North") — never a GUID or technical id.
    public string Label { get; init; } = string.Empty;
    // Owning team name in Profile/HR roster vocabulary; null = open area with no team priority.
    public string? OwningTeam { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// SEAT001 (#783) — one bookable seat in the simple grid model: a stable id, a row/column
/// position inside its area, an employee-safe label, and basic capabilities. Date/range
/// unavailability is expressed through audited <see cref="SeatBlock"/>s rather than fields
/// here; finer time-of-day availability windows arrive with the allocation slice (SEAT003)
/// if a tenant policy needs them.
/// </summary>
public sealed record Seat
{
    public string SeatId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string AreaId { get; init; } = string.Empty;
    // Grid position within the area — a simple grid, not a floorplan.
    public int Row { get; init; }
    public int Column { get; init; }
    // Employee-safe display name (e.g. "Team A-04").
    public string Label { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsAccessible { get; init; }
    public bool HasMonitor { get; init; }
    public bool HasDockingStation { get; init; }
}

/// <summary>
/// The whole seat grid for one location, replaced atomically like the parking slot list.
/// </summary>
public sealed record SeatMap
{
    public IReadOnlyList<SeatArea> Areas { get; init; } = [];
    public IReadOnlyList<Seat> Seats { get; init; } = [];
}

/// <summary>
/// Business-safe reason category for blocking a seat. Categories are employee-visible on the
/// seat map; the optional free-text note on the block is HR/admin-only evidence.
/// </summary>
public enum SeatBlockReason
{
    Maintenance = 0,
    Reserved = 1,
    Facilities = 2,
    Other = 3,
}

/// <summary>
/// SEAT001 (#783) — an HR/Admin block that removes a seat from allocation for a date or date
/// range. Blocks affect capacity and fairness, so every add/remove is recorded in the seat-map
/// change history with the acting user.
/// </summary>
public sealed record SeatBlock
{
    public string BlockId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string SeatId { get; init; } = string.Empty;
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public SeatBlockReason Reason { get; init; }
    // HR/admin-only context — never exposed on the employee-safe map.
    public string? Note { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
