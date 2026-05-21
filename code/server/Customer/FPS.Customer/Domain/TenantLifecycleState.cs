namespace FPS.Customer.Domain;

public enum TenantLifecycleState
{
    Draft,
    Configured,
    Seeded,
    Ready,
    Suspended,
    Archived,
}
