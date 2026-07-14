using FPS.Configuration.Domain;

namespace FPS.Configuration.Application;

/// <summary>
/// SEAT001 (#783) — tenant seat-grid configuration: whole-map replace (mirroring the parking
/// slot list), HR/Admin date-range seat blocks, and the audited change history both write to.
/// </summary>
public sealed class SeatMapService(
    ISeatMapRepository mapRepository,
    ISeatBlockRepository blockRepository,
    ISeatMapChangeRepository changeRepository)
{
    // Grid bounds: generous for any real office floor, tight enough that a typo cannot
    // create a pathological grid.
    private const int MaxRowOrColumn = 500;
    private const int MaxLabelLength = 80;
    private const int MaxNoteLength = 300;
    // A block may cover at most two years — longer unavailability is a map change
    // (deactivate the seat), not a block.
    private const int MaxBlockDays = 731;

    public Task<SeatMap> GetMapAsync(string tenantId, string locationId, CancellationToken ct)
        => mapRepository.GetByLocationAsync(tenantId, locationId, ct);

    public Task<IReadOnlyList<SeatBlock>> GetBlocksAsync(string tenantId, string locationId, CancellationToken ct)
        => blockRepository.GetByLocationAsync(tenantId, locationId, ct);

    public Task<IReadOnlyList<SeatMapChangeRecord>> GetChangeHistoryAsync(string tenantId, string locationId, int limit, CancellationToken ct)
        => changeRepository.GetHistoryAsync(tenantId, locationId, limit, ct);

    public async Task<IReadOnlyList<string>> ReplaceAsync(
        string tenantId, string locationId, SeatMap map,
        string changedByUserId, string? changeReason, CancellationToken ct)
    {
        var errors = Validate(map);
        if (errors.Count > 0) return errors;

        await mapRepository.ReplaceLocationSeatMapAsync(tenantId, locationId, map, ct);
        await changeRepository.RecordAsync(new SeatMapChangeRecord
        {
            TenantId = tenantId,
            LocationId = locationId,
            ChangeType = SeatMapChangeRecord.TypeMapReplaced,
            ChangedByUserId = changedByUserId,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason.Trim(),
            AreaCount = map.Areas.Count,
            SeatCount = map.Seats.Count,
        }, ct);
        return [];
    }

    public async Task<(string? blockId, IReadOnlyList<string> errors)> AddBlockAsync(
        string tenantId, string locationId, string seatId,
        DateOnly fromDate, DateOnly toDate, SeatBlockReason reason, string? note,
        string createdByUserId, CancellationToken ct)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(seatId))
            errors.Add("seatId is required.");
        if (toDate < fromDate)
            errors.Add("toDate must not be before fromDate.");
        else if (toDate.DayNumber - fromDate.DayNumber > MaxBlockDays)
            errors.Add($"A block may cover at most {MaxBlockDays} days. Deactivate the seat in the map for longer unavailability.");
        if (note is { Length: > MaxNoteLength })
            errors.Add($"note must not exceed {MaxNoteLength} characters.");

        if (errors.Count == 0)
        {
            var map = await mapRepository.GetByLocationAsync(tenantId, locationId, ct);
            if (!map.Seats.Any(s => s.SeatId.Equals(seatId, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"Seat '{seatId}' does not exist at this location.");
        }
        if (errors.Count > 0) return (null, errors);

        var block = new SeatBlock
        {
            BlockId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            LocationId = locationId,
            SeatId = seatId,
            FromDate = fromDate,
            ToDate = toDate,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await blockRepository.AddAsync(block, ct);
        await changeRepository.RecordAsync(new SeatMapChangeRecord
        {
            TenantId = tenantId,
            LocationId = locationId,
            ChangeType = SeatMapChangeRecord.TypeSeatBlocked,
            ChangedByUserId = createdByUserId,
            ChangedAt = DateTimeOffset.UtcNow,
            SeatId = seatId,
            BlockedFrom = fromDate,
            BlockedTo = toDate,
            BlockReason = reason,
        }, ct);
        return (block.BlockId, []);
    }

    /// <summary>Returns false when the block does not exist (caller maps to 404).</summary>
    public async Task<bool> RemoveBlockAsync(
        string tenantId, string locationId, string blockId,
        string removedByUserId, string? changeReason, CancellationToken ct)
    {
        var existing = (await blockRepository.GetByLocationAsync(tenantId, locationId, ct))
            .FirstOrDefault(b => b.BlockId == blockId);
        if (existing is null) return false;

        var removed = await blockRepository.RemoveAsync(tenantId, locationId, blockId, ct);
        if (!removed) return false;

        await changeRepository.RecordAsync(new SeatMapChangeRecord
        {
            TenantId = tenantId,
            LocationId = locationId,
            ChangeType = SeatMapChangeRecord.TypeSeatUnblocked,
            ChangedByUserId = removedByUserId,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason.Trim(),
            SeatId = existing.SeatId,
            BlockedFrom = existing.FromDate,
            BlockedTo = existing.ToDate,
            BlockReason = existing.Reason,
        }, ct);
        return true;
    }

    public static IReadOnlyList<string> Validate(SeatMap map)
    {
        var errors = new List<string>();

        var areaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var area in map.Areas)
        {
            if (string.IsNullOrWhiteSpace(area.AreaId))
                errors.Add("Each area must have a non-empty areaId.");
            else if (!areaIds.Add(area.AreaId))
                errors.Add($"Duplicate areaId: {area.AreaId}.");

            if (string.IsNullOrWhiteSpace(area.Label))
                errors.Add($"Area {area.AreaId}: label is required.");
            else if (area.Label.Length > MaxLabelLength)
                errors.Add($"Area {area.AreaId}: label must not exceed {MaxLabelLength} characters.");

            if (area.OwningTeam is { Length: > MaxLabelLength })
                errors.Add($"Area {area.AreaId}: owningTeam must not exceed {MaxLabelLength} characters.");
        }

        var seatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var positions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seat in map.Seats)
        {
            if (string.IsNullOrWhiteSpace(seat.SeatId))
                errors.Add("Each seat must have a non-empty seatId.");
            else if (!seatIds.Add(seat.SeatId))
                errors.Add($"Duplicate seatId: {seat.SeatId}.");

            if (string.IsNullOrWhiteSpace(seat.Label))
                errors.Add($"Seat {seat.SeatId}: label is required.");
            else if (seat.Label.Length > MaxLabelLength)
                errors.Add($"Seat {seat.SeatId}: label must not exceed {MaxLabelLength} characters.");

            if (string.IsNullOrWhiteSpace(seat.AreaId) || !areaIds.Contains(seat.AreaId))
                errors.Add($"Seat {seat.SeatId}: areaId '{seat.AreaId}' does not match any area in the map.");

            if (seat.Row < 0 || seat.Row > MaxRowOrColumn || seat.Column < 0 || seat.Column > MaxRowOrColumn)
                errors.Add($"Seat {seat.SeatId}: row and column must be between 0 and {MaxRowOrColumn}.");
            else if (!string.IsNullOrWhiteSpace(seat.AreaId) && !positions.Add($"{seat.AreaId}|{seat.Row}|{seat.Column}"))
                errors.Add($"Seat {seat.SeatId}: another seat already occupies row {seat.Row}, column {seat.Column} in area {seat.AreaId}.");
        }

        return errors;
    }
}
