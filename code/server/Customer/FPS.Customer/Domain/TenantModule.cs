namespace FPS.Customer.Domain;

/// <summary>
/// PLAT007B — a product module a tenant can run. A tenant has exactly one <b>primary</b> module
/// (its default landing / navigation emphasis) and may enable additional modules alongside it.
/// Parking is the default and, today, the only implemented module; Seats is the contract seats
/// (#710) builds on. New enum values append at the end so stored ordinals stay stable — and
/// <see cref="Parking"/> stays 0 so a tenant persisted before this field defaults to Parking.
/// </summary>
public enum TenantModule
{
    Parking = 0,
    Seats = 1,
}
