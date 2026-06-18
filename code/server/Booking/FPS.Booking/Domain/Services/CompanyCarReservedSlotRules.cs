namespace FPS.Booking.Domain.Services;

public static class CompanyCarReservedSlotRules
{
    public const string MissingReservedSlotReason =
        "Company-car request rejected: no reserved slot is configured for this requestor.";
    public const string InactiveReservedSlotReason =
        "Company-car request rejected: the assigned reserved slot is inactive.";
    public const string IncompatibleReservedSlotReason =
        "Company-car request rejected: the assigned reserved slot is incompatible with vehicle requirements.";
    public const string ReservedSlotAlreadyConsumedReason =
        "Company-car request rejected: the assigned reserved slot was already allocated in this draw.";

    public static CompanyCarReservedSlotResult Resolve(
        UserId requestorId,
        VehicleInformation vehicle,
        IReadOnlyList<AvailableSlot> allSlots,
        Func<AvailableSlot, bool>? isAvailable = null)
    {
        var reservedForRequestor = allSlots
            .Where(s => s.IsReservedFor(requestorId))
            .ToList();

        if (reservedForRequestor.Count == 0)
            return CompanyCarReservedSlotResult.Rejected(MissingReservedSlotReason);

        var activeReserved = reservedForRequestor
            .Where(s => s.IsActive)
            .ToList();

        if (activeReserved.Count == 0)
            return CompanyCarReservedSlotResult.Rejected(InactiveReservedSlotReason);

        var compatibleReserved = activeReserved
            .Where(s => s.CanAccommodate(vehicle))
            .OrderBy(s => s.SlotId.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (compatibleReserved.Count == 0)
            return CompanyCarReservedSlotResult.Rejected(IncompatibleReservedSlotReason);

        var availablePredicate = isAvailable ?? (_ => true);
        var selected = compatibleReserved.FirstOrDefault(availablePredicate);
        if (selected is null)
            return CompanyCarReservedSlotResult.Rejected(ReservedSlotAlreadyConsumedReason);

        return CompanyCarReservedSlotResult.Allocated(selected);
    }
}

public sealed record CompanyCarReservedSlotResult(AvailableSlot? Slot, string? RejectionReason)
{
    public static CompanyCarReservedSlotResult Allocated(AvailableSlot slot)
        => new(slot, null);

    public static CompanyCarReservedSlotResult Rejected(string reason)
        => new(null, reason);
}
