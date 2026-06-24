namespace FPS.Customer.Domain;

public sealed record TenantDemoSeedEvent(
    string ActorHash,
    string DatasetVersion,
    DateTimeOffset SeededAt,
    string Reason);
