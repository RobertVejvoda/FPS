namespace FPS.Booking.Domain;

/// <summary>
/// PLAT-seats (#710) — the kind of scarce workplace resource a booking request is for. Parking is
/// the original and default resource; Seats is the first additional module (a team seat/desk for a
/// workday). The Draw itself is resource-agnostic — it partitions by (tenant, location, date, time
/// slot) — so this is carried on the request and outcome as evidence and to keep parking and seat
/// requests, labels, and reports from mixing. Parking stays ordinal 0 so anything persisted before
/// this field defaults to Parking.
/// </summary>
public enum ResourceType
{
    Parking = 0,
    Seats = 1,
}
