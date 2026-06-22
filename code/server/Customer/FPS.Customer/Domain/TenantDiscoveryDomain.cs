namespace FPS.Customer.Domain;

public sealed record TenantDiscoveryDomain(
    string Domain,
    string RegisteredByHash,
    DateTimeOffset RegisteredAt);
