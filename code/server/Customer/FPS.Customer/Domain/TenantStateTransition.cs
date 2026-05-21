namespace FPS.Customer.Domain;

public sealed record TenantStateTransition(
    TenantLifecycleState From,
    TenantLifecycleState To,
    string ActorId,
    DateTimeOffset OccurredAt,
    string? Reason,
    string? Evidence);
