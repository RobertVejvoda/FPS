using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Configuration.Controllers;

// SEAT001 (#783) — tenant seat-grid configuration. Class-level [Authorize] only requires
// authentication; role guards live on each HR/admin action so the employee-safe
// /seat-map/map endpoint can be reached by any authenticated tenant user (same additive
// [Authorize] rule as ParkingSlotController).
[ApiController]
[Authorize]
public sealed class SeatMapController(SeatMapService service, ICurrentUser currentUser) : ControllerBase
{
    private const string HrAdminRoles = $"{ConfigurationRoles.Admin},{ConfigurationRoles.HrManager}";

    [HttpGet("/configuration/locations/{locationId}/seat-map")]
    [Authorize(Roles = HrAdminRoles)]
    public async Task<IActionResult> GetSeatMap(string locationId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var map = await service.GetMapAsync(currentUser.TenantId, locationId, ct);
        var blocks = await service.GetBlocksAsync(currentUser.TenantId, locationId, ct);
        return Ok(new SeatMapResponse(
            map.Areas.Select(SeatAreaDto.FromDomain).ToList(),
            map.Seats.Select(SeatDto.FromDomain).ToList(),
            blocks.Select(SeatBlockDto.FromDomain).ToList()));
    }

    [HttpPut("/configuration/locations/{locationId}/seat-map")]
    [Authorize(Roles = HrAdminRoles)]
    public async Task<IActionResult> PutSeatMap(string locationId, [FromBody] PutSeatMapRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var map = new SeatMap
        {
            Areas = request.Areas.Select(a => a.ToDomain(currentUser.TenantId, locationId)).ToList(),
            Seats = request.Seats.Select(s => s.ToDomain(currentUser.TenantId, locationId)).ToList(),
        };

        var errors = await service.ReplaceAsync(currentUser.TenantId, locationId, map, currentUser.UserId, request.ChangeReason, ct);
        return errors.Count > 0 ? BadRequest(new { errors }) : NoContent();
    }

    [HttpGet("/configuration/locations/{locationId}/seat-map/history")]
    [Authorize(Roles = HrAdminRoles)]
    public async Task<IActionResult> GetSeatMapHistory(string locationId, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        limit = Math.Clamp(limit, 1, 100);
        var history = await service.GetChangeHistoryAsync(currentUser.TenantId, locationId, limit, ct);
        return Ok(history);
    }

    [HttpPost("/configuration/locations/{locationId}/seat-blocks")]
    [Authorize(Roles = HrAdminRoles)]
    public async Task<IActionResult> AddSeatBlock(string locationId, [FromBody] AddSeatBlockRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        if (!Enum.TryParse<SeatBlockReason>(request.Reason, ignoreCase: true, out var reason))
            return BadRequest(new { errors = new[] { $"Unknown block reason: {request.Reason}. Use Maintenance, Reserved, Facilities, or Other." } });

        var (blockId, errors) = await service.AddBlockAsync(
            currentUser.TenantId, locationId, request.SeatId,
            request.FromDate, request.ToDate, reason, request.Note,
            currentUser.UserId, ct);

        return errors.Count > 0 ? BadRequest(new { errors }) : Ok(new { blockId });
    }

    [HttpDelete("/configuration/locations/{locationId}/seat-blocks/{blockId}")]
    [Authorize(Roles = HrAdminRoles)]
    public async Task<IActionResult> RemoveSeatBlock(string locationId, string blockId, [FromQuery] string? reason, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var removed = await service.RemoveBlockAsync(currentUser.TenantId, locationId, blockId, currentUser.UserId, reason, ct);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>
    /// Employee-safe seat map. Reached via the class-level [Authorize] (no role guard), so any
    /// authenticated tenant user can read it for seat preference selection. Exposes only
    /// business-safe data: labels, grid positions, capabilities, owning team, and blocked date
    /// ranges with their business reason category — never block notes, acting users, block ids,
    /// or any other user's data.
    /// </summary>
    [HttpGet("/configuration/locations/{locationId}/seat-map/map")]
    [ProducesResponseType(typeof(EmployeeSeatMapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEmployeeSeatMap(string locationId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var map = await service.GetMapAsync(currentUser.TenantId, locationId, ct);
        var blocks = await service.GetBlocksAsync(currentUser.TenantId, locationId, ct);

        // Only current and future blocks are relevant for choosing a seat; expired block
        // history stays HR/admin-only evidence.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var visibleBlocks = blocks
            .Where(b => b.ToDate >= today)
            .ToLookup(b => b.SeatId, StringComparer.OrdinalIgnoreCase);

        var response = new EmployeeSeatMapResponse(
            map.Areas.Select(a => new EmployeeSeatAreaDto(a.AreaId, a.Label, a.OwningTeam, a.IsActive)).ToList(),
            map.Seats.Select(s => new EmployeeSeatDto(
                s.SeatId, s.AreaId, s.Row, s.Column, s.Label, s.IsActive,
                s.IsAccessible, s.HasMonitor, s.HasDockingStation,
                visibleBlocks[s.SeatId]
                    .OrderBy(b => b.FromDate)
                    .Select(b => new EmployeeSeatBlockDto(b.FromDate, b.ToDate, b.Reason.ToString()))
                    .ToList())).ToList());

        return Ok(response);
    }
}

// ── Admin request/response DTOs ───────────────────────────────────────────────

public sealed record PutSeatMapRequest(
    IReadOnlyList<SeatAreaInputDto> Areas,
    IReadOnlyList<SeatInputDto> Seats,
    string? ChangeReason);

public sealed record SeatAreaInputDto(
    string AreaId,
    string Label,
    string? OwningTeam,
    bool IsActive)
{
    internal SeatArea ToDomain(string tenantId, string locationId) => new()
    {
        AreaId = AreaId,
        TenantId = tenantId,
        LocationId = locationId,
        Label = Label,
        OwningTeam = string.IsNullOrWhiteSpace(OwningTeam) ? null : OwningTeam.Trim(),
        IsActive = IsActive,
    };
}

public sealed record SeatInputDto(
    string SeatId,
    string AreaId,
    int Row,
    int Column,
    string Label,
    bool IsActive,
    bool IsAccessible = false,
    bool HasMonitor = false,
    bool HasDockingStation = false)
{
    internal Seat ToDomain(string tenantId, string locationId) => new()
    {
        SeatId = SeatId,
        TenantId = tenantId,
        LocationId = locationId,
        AreaId = AreaId,
        Row = Row,
        Column = Column,
        Label = Label,
        IsActive = IsActive,
        IsAccessible = IsAccessible,
        HasMonitor = HasMonitor,
        HasDockingStation = HasDockingStation,
    };
}

public sealed record AddSeatBlockRequest(
    string SeatId,
    DateOnly FromDate,
    DateOnly ToDate,
    string Reason,
    string? Note);

public sealed record SeatMapResponse(
    IReadOnlyList<SeatAreaDto> Areas,
    IReadOnlyList<SeatDto> Seats,
    IReadOnlyList<SeatBlockDto> Blocks);

public sealed record SeatAreaDto(string AreaId, string Label, string? OwningTeam, bool IsActive)
{
    internal static SeatAreaDto FromDomain(SeatArea a) => new(a.AreaId, a.Label, a.OwningTeam, a.IsActive);
}

public sealed record SeatDto(
    string SeatId, string AreaId, int Row, int Column, string Label,
    bool IsActive, bool IsAccessible, bool HasMonitor, bool HasDockingStation)
{
    internal static SeatDto FromDomain(Seat s) => new(
        s.SeatId, s.AreaId, s.Row, s.Column, s.Label,
        s.IsActive, s.IsAccessible, s.HasMonitor, s.HasDockingStation);
}

public sealed record SeatBlockDto(
    string BlockId, string SeatId, DateOnly FromDate, DateOnly ToDate,
    string Reason, string? Note, string CreatedByUserId, DateTimeOffset CreatedAt)
{
    internal static SeatBlockDto FromDomain(SeatBlock b) => new(
        b.BlockId, b.SeatId, b.FromDate, b.ToDate,
        b.Reason.ToString(), b.Note, b.CreatedByUserId, b.CreatedAt);
}

// ── Employee-safe map DTOs ────────────────────────────────────────────────────

public sealed record EmployeeSeatMapResponse(
    IReadOnlyList<EmployeeSeatAreaDto> Areas,
    IReadOnlyList<EmployeeSeatDto> Seats);

public sealed record EmployeeSeatAreaDto(string AreaId, string Label, string? OwningTeam, bool IsActive);

public sealed record EmployeeSeatDto(
    string SeatId, string AreaId, int Row, int Column, string Label,
    bool IsActive, bool IsAccessible, bool HasMonitor, bool HasDockingStation,
    IReadOnlyList<EmployeeSeatBlockDto> Blocks);

public sealed record EmployeeSeatBlockDto(DateOnly FromDate, DateOnly ToDate, string Reason);
